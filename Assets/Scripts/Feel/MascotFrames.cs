using System;
using UnityEngine;
// Eri'nin (sera maskotu) el çizimi kareleri: hâl başına bir tablo, 16 kare. Gezegendeki Eri (GreenhouseMascot) ve diyalog
// kutusunun portresi aynı sprite'ları paylaşır.
// Hâller (Faz 3, 5 Kasım 2026): hikâye aşaması 0–1 mutlu, 2 üzgün (gri), 3 şeytani (kırmızı) → Resources/GreenhouseMascotFrames_<Hâl>.
// Bellekte yalnız güncel hâlin tablosu durur: aşama değişince (DialogueProgress.StageChanged) eskisi bırakılır, yenisi yüklenir,
// Changed yayınlanır (görünen sprite'lar yenilenir). Hâl tablosu yoksa eski tek tablo (GreenhouseMascotFrames) kullanılır.
// Kare sınırları araçla ölçüldü (Tools/Art/EriMoods/eri_frames.ps1): kareler düzgün ızgarada değil.
public static class MascotFrames
{
    public const int Count = 16, Front = 8;
    // Boyutlar (Size) bugünkü 1254 piksellik tabloya göre verilir: 2880'lik hâl tabloları aynı boyda çizilir, yerleşim değişmez.
    public const float ReferenceSize = 1254f;

    private sealed class Sheet
    {
        public string resource;
        public float size;
        public Vector4[] bounds;   // (x0, y0, x1, y1) kaynak piksel, y yukarıdan, bitiş hariç
    }

    // Measured alpha bounds in the 1254px source atlas; generated sheets are not
    // perfectly uniform grids. Keep 2px padding and a common feet pivot.
    private static readonly Sheet Legacy = new()
    {
        resource = "GreenhouseMascotFrames", size = 1254f, bounds = new[]
        {
            new Vector4(19,23,329,321),new Vector4(353,35,617,318),new Vector4(654,27,925,319),new Vector4(956,27,1235,319),
            new Vector4(25,339,324,622),new Vector4(345,346,633,620),new Vector4(670,330,945,623),new Vector4(970,333,1247,625),
            new Vector4(22,638,313,923),new Vector4(331,638,629,923),new Vector4(648,637,941,923),new Vector4(960,638,1244,925),
            new Vector4(26,939,331,1230),new Vector4(366,938,629,1224),new Vector4(651,933,931,1230),new Vector4(947,937,1245,1227)
        }
    };

    // Hâl tabloları (2880 px; eri_frames.ps1 çıktısı).
    private static readonly Sheet[] Moods =
    {
        new() { resource = "GreenhouseMascotFrames_Mutlu", size = 2880f, bounds = new[]
        {
            new Vector4(45,47,758,738),new Vector4(810,73,1424,734),new Vector4(1504,58,2137,737),new Vector4(2200,59,2845,735),
            new Vector4(61,780,745,1429),new Vector4(795,790,1460,1424),new Vector4(1541,758,2174,1431),new Vector4(2235,767,2872,1437),
            new Vector4(47,1463,724,2122),new Vector4(762,1463,1447,2123),new Vector4(1490,1461,2164,2122),new Vector4(2212,1467,2865,2127),
            new Vector4(60,2161,758,2827),new Vector4(836,2158,1448,2815),new Vector4(1497,2149,2144,2828),new Vector4(2181,2154,2866,2824)
        } },
        new() { resource = "GreenhouseMascotFrames_Uzgun", size = 2880f, bounds = new[]
        {
            new Vector4(43,37,753,738),new Vector4(804,63,1430,741),new Vector4(1505,49,2146,743),new Vector4(2213,51,2853,739),
            new Vector4(52,771,751,1431),new Vector4(793,778,1468,1430),new Vector4(1547,764,2185,1434),new Vector4(2236,768,2872,1435),
            new Vector4(40,1460,743,2129),new Vector4(764,1460,1453,2127),new Vector4(1480,1456,2182,2127),new Vector4(2223,1459,2873,2127),
            new Vector4(47,2155,762,2829),new Vector4(829,2155,1454,2818),new Vector4(1493,2152,2149,2829),new Vector4(2194,2154,2870,2826)
        } },
        new() { resource = "GreenhouseMascotFrames_Seytani", size = 2880f, bounds = new[]
        {
            new Vector4(40,44,759,738),new Vector4(806,67,1429,737),new Vector4(1501,54,2144,738),new Vector4(2196,57,2841,736),
            new Vector4(56,777,750,1430),new Vector4(789,790,1457,1425),new Vector4(1540,758,2178,1429),new Vector4(2230,764,2871,1434),
            new Vector4(44,1457,729,2123),new Vector4(757,1461,1448,2123),new Vector4(1481,1457,2170,2123),new Vector4(2203,1461,2861,2128),
            new Vector4(50,2153,756,2829),new Vector4(835,2156,1446,2816),new Vector4(1496,2151,2146,2827),new Vector4(2192,2152,2864,2825)
        } },
    };
    public static readonly string[] MoodNames = { "Mutlu", "Uzgun", "Seytani" };

    private static Sheet sheet;
    private static Texture2D texture;
    private static Sprite[] sprites;
    private static int loadedMood = -1;
    private static bool subscribed;

    // Görünen tablo değişti: kareyi gösteren yerler sprite'ı yeniden ister.
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sheet = null; texture = null; sprites = null; loadedMood = -1; subscribed = false; Changed = null;
    }

    // Hikâye aşamasının hâli: 0–1 mutlu, 2 üzgün, 3 şeytani.
    public static int MoodFor(int stage) => stage >= DialogueProgress.Revealed ? 2 : stage >= DialogueProgress.Sad ? 1 : 0;
    public static int Mood => MoodFor(DialogueProgress.Stage);
    public static string MoodName => MoodNames[Mood];
    // Bellekteki tablo (test: yalnız bir tane).
    public static Texture2D LoadedTexture => texture;
    public static int LoadedMood => loadedMood;

    public static bool Available => Sprites != null;

    public static Sprite Get(int frame)
    {
        Sprite[] all = Sprites;
        return all == null ? null : all[Mathf.Clamp(frame, 0, Count - 1)];
    }

    // Karenin boyu (dolgu dahil), 1254'lük referans ölçekte: yürüyüş ve kutu boyutlandırması için.
    public static Vector2 Size(int frame)
    {
        Sheet s = sheet ?? (Sprites != null ? sheet : Legacy);
        Vector4 b = s.bounds[Mathf.Clamp(frame, 0, Count - 1)];
        float k = ReferenceSize / s.size;
        return new Vector2((b.z - b.x + 4) * k, (b.w - b.y + 4) * k);
    }

    private static Sprite[] Sprites
    {
        get
        {
            int mood = Mood;
            if (loadedMood != mood) Load(mood);
            return sprites;
        }
    }

    private static void OnStageChanged()
    {
        if (Mood == loadedMood) return;
        Load(Mood);
        Changed?.Invoke();
    }

    // Eski tablo bırakılır (sprite'lar yok edilir, doku boşaltılır), yenisi yüklenir.
    private static void Load(int mood)
    {
        if (!subscribed) { DialogueProgress.StageChanged += OnStageChanged; subscribed = true; }
        Release();
        loadedMood = mood;
        Sheet s = Moods[mood];
        Texture2D tex = Resources.Load<Texture2D>(s.resource);
        if (tex == null) { s = Legacy; tex = Resources.Load<Texture2D>(s.resource); }
        if (tex == null) return;
        sheet = s;
        texture = tex;
        sprites = new Sprite[Count];
        for (int i = 0; i < Count; i++)
        {
            Vector4 b = s.bounds[i];
            var rect = new Rect((b.x - 2) * tex.width / s.size, (s.size - b.w - 2) * tex.height / s.size,
                (b.z - b.x + 4) * tex.width / s.size, (b.w - b.y + 4) * tex.height / s.size);
            sprites[i] = Sprite.Create(tex, rect, new Vector2(.5f, 0), 100, 0, SpriteMeshType.FullRect);
            sprites[i].name = $"Mascot {MoodNames[mood]} frame {i}";
        }
    }

    // Sprite'lar çalışma anında üretildi (asset değil): hemen yok edilir; ertelemeli silme dokuyu kare sonuna kadar tutardı, iki tablo
    // aynı anda bellekte kalırdı. Gösteren yerler Changed ile aynı anda yenilenir.
    private static void Release()
    {
        if (sprites != null)
            foreach (Sprite s in sprites)
                if (s != null) UnityEngine.Object.DestroyImmediate(s);
        sprites = null;
        if (texture != null) Resources.UnloadAsset(texture);
        texture = null;
        sheet = null;
    }
}
