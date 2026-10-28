using UnityEngine;

// Eri'nin (sera maskotu) el çizimi kareleri: tek görsel (Resources/GreenhouseMascotFrames), 16 kare. Gezegendeki Eri
// (GreenhouseMascot) ve diyalog kutusunun portresi aynı sprite'ları paylaşır: bir kez kurulur, oyun boyunca yaşar.
public static class MascotFrames
{
    public const int Count = 16, Front = 8;
    // Measured alpha bounds in the 1254px source atlas; generated sheets are not
    // perfectly uniform grids. Keep 2px padding and a common feet pivot.
    public static readonly Vector4[] Bounds = {
        new Vector4(19,23,329,321),new Vector4(353,35,617,318),new Vector4(654,27,925,319),new Vector4(956,27,1235,319),
        new Vector4(25,339,324,622),new Vector4(345,346,633,620),new Vector4(670,330,945,623),new Vector4(970,333,1247,625),
        new Vector4(22,638,313,923),new Vector4(331,638,629,923),new Vector4(648,637,941,923),new Vector4(960,638,1244,925),
        new Vector4(26,939,331,1230),new Vector4(366,938,629,1224),new Vector4(651,933,931,1230),new Vector4(947,937,1245,1227)
    };
    private static Sprite[] sprites;
    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { sprites = null; loaded = false; }

    public static bool Available => Sprites != null;

    public static Sprite Get(int frame)
    {
        Sprite[] all = Sprites;
        return all == null ? null : all[Mathf.Clamp(frame, 0, Count - 1)];
    }

    // Karenin kaynak görseldeki boyu (piksel, dolgu dahil): yürüyüş boyutlandırması için.
    public static Vector2 Size(int frame)
    {
        Vector4 b = Bounds[Mathf.Clamp(frame, 0, Count - 1)];
        return new Vector2(b.z - b.x + 4, b.w - b.y + 4);
    }

    private static Sprite[] Sprites
    {
        get
        {
            if (loaded) return sprites;
            loaded = true;
            var texture = Resources.Load<Texture2D>("GreenhouseMascotFrames");
            if (texture == null) return null;
            sprites = new Sprite[Count];
            for (int i = 0; i < Count; i++)
            {
                Vector4 b = Bounds[i];
                var rect = new Rect((b.x - 2) * texture.width / 1254f, (1254 - b.w - 2) * texture.height / 1254f,
                    (b.z - b.x + 4) * texture.width / 1254f, (b.w - b.y + 4) * texture.height / 1254f);
                sprites[i] = Sprite.Create(texture, rect, new Vector2(.5f, 0), 100, 0, SpriteMeshType.FullRect);
                sprites[i].name = "Mascot frame " + i;
            }
            return sprites;
        }
    }
}
