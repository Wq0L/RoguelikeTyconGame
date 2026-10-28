using UnityEngine;

// Konuşma kutusunun kodla üretilen dokuları (dış kaynak yok): perde vinyeti ve köşe tram noktaları (kutunun içinde ve perdenin üst köşelerinde).
// Her biri ilk istekte bir kez kurulur ve oyun boyunca paylaşılır (CardSelectionVignette ile aynı düzen); karede iş yok.
public static class DialogueArt
{
    // Vinyet merkezi kutunun durduğu alt ortada: kutunun çevresi az, ekran kenarları çok kararır.
    public const float VignetteCenterY = .26f, VignetteInner = .3f, VignetteOuter = 1.15f, VignetteFloor = .45f;
    private static Texture2D vignette, halftone, halftoneFine;
    private static Sprite eriSilhouette;
    private static bool eriSilhouetteLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { vignette = null; halftone = null; halftoneFine = null; eriSilhouette = null; eriSilhouetteLoaded = false; }

    // Eri'nin konuşma karesinin (DialogueBox.EriFrame) beyaz silüeti: Resources/Dialogue/EriSilhouette.png, GreenhouseMascotFrames'in
    // 0. karesinden türetildi (renk beyaz, saydamlık ×3: camın yarı saydam yerleri dolu). Kontur ve kâğıt rengi altlık için; bir kez kurulur.
    public static Sprite EriSilhouette
    {
        get
        {
            if (eriSilhouetteLoaded) return eriSilhouette;
            eriSilhouetteLoaded = true;
            var texture = Resources.Load<Texture2D>("Dialogue/EriSilhouette");
            if (texture != null)
                eriSilhouette = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, 0f), 100f, 0, SpriteMeshType.FullRect);
            return eriSilhouette;
        }
    }

    // Beyaz; saydamlık merkezde VignetteFloor, kenarda 1. Renk RawImage'tan verilir.
    public static Texture2D Vignette
    {
        get
        {
            if (vignette != null) return vignette;
            const int w = 256, h = 144;
            vignette = New("Dialogue Vignette", w, h);
            var pixels = new Color32[w * h];
            var ink = new Color32(255, 255, 255, 0);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = ((x + .5f) / w - .5f) * (16f / 9f) / 1.05f, v = ((y + .5f) / h - VignetteCenterY) / .75f;
                float t = Mathf.InverseLerp(VignetteInner, VignetteOuter, Mathf.Sqrt(u * u + v * v));
                t = t * t * (3f - 2f * t);
                ink.a = (byte)Mathf.RoundToInt(Mathf.Lerp(VignetteFloor, 1f, t) * 255f);
                pixels[y * w + x] = ink;
            }
            vignette.SetPixels32(pixels);
            vignette.Apply(false, true);
            return vignette;
        }
    }

    // Köşe tramı: köşede (sol alt piksel) iri, uzaklaştıkça küçülen noktalar. Diğer köşeler uvRect çevrilerek kullanılır.
    public static Texture2D Halftone => halftone != null ? halftone : halftone = MakeHalftone("Dialogue Halftone", 20, 8.6f, 1.4f);

    // İnce tram (perdenin üst köşeleri, "not" görünüşü): küçük, sık noktalar; köşeden içeri hızla seyrelir (baskı tramı gibi).
    public static Texture2D HalftoneFine => halftoneFine != null ? halftoneFine : halftoneFine = MakeHalftone("Dialogue Halftone Fine", 12, 5.6f, 2.2f);

    private static Texture2D MakeHalftone(string name, int cell, float maxRadius, float power)
    {
        {
            const int size = 256;
            Texture2D texture = New(name, size, size);
            var pixels = new Color32[size * size];
            var ink = new Color32(255, 255, 255, 0);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Satırlar yarım hücre kaydırılır (baskı tramı gibi çapraz dizilim).
                int row = y / cell;
                float cx = (Mathf.Floor((x + (row % 2) * cell * .5f) / cell) + .5f) * cell - (row % 2) * cell * .5f;
                float cy = (row + .5f) * cell;
                float falloff = 1f - Mathf.Clamp01(Mathf.Sqrt(cx * cx + cy * cy) / (size * 1.05f));
                float radius = maxRadius * Mathf.Pow(falloff, power);
                float dx = x + .5f - cx, dy = y + .5f - cy;
                ink.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + .5f) * 255f);
                pixels[y * size + x] = ink;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }

    private static Texture2D New(string name, int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false)
    {
        name = name,
        wrapMode = TextureWrapMode.Clamp,
        filterMode = FilterMode.Bilinear,
        hideFlags = HideFlags.DontSave,
    };
}
