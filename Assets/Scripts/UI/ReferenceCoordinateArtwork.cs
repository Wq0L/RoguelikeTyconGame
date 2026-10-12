using UnityEngine;

// Each label keeps its individual reference silhouette. Returned sprites/textures
// belong to the caller; Resources textures remain untouched.
public static class ReferenceCoordinateArtwork
{
    public static Sprite Create(int variant, bool world, bool gold = false)
    {
        int index = variant >= 1000 ? variant - 1000 : variant;
        if (index < 0 || index > 10) return null;
        string key = variant >= 1000 ? (index + 1).ToString("00") : ((char)('A' + index)).ToString();
        var source = Resources.Load<Texture2D>("CoordinateReference/" + (world ? "World/" : "UI/") + key);
        if (source == null || !source.isReadable) return null;
        var pixels = source.GetPixels32();
        int left = source.width, right = -1, bottom = source.height, top = -1;
        for (int y = 0; y < source.height; y++)
        for (int x = 0; x < source.width; x++)
        {
            int i = y * source.width + x;
            if (pixels[i].a > 8)
            {
                left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
            }
            if (!gold || pixels[i].a == 0) continue;
            Color color = pixels[i];
            Color.RGBToHSV(color, out float h, out float s, out float v);
            // Recolour only printed blue. White lettering and dark ink stay intact.
            if (h > .48f && h < .70f && s > .25f && v > .16f)
            {
                Color recolored = Color.HSVToRGB(.125f, s, Mathf.Min(1f, v * 1.18f));
                recolored.a = color.a;
                pixels[i] = recolored;
            }
        }
        if (right < left) return null;
        var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        texture.name = "Reference coordinate " + key + (gold ? " gold" : " blue");
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var rect = new Rect(left, bottom, right - left + 1, top - bottom + 1);
        var sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), rect.height);
        sprite.name = texture.name;
        return sprite;
    }
}
