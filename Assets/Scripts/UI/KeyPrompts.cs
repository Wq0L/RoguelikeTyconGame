using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

// Yazılardaki tuş adları yerine comic tuş ikonu. Kaynak: Kenney "Input Prompts" 1.5A (CC0), oyunun renklerine boyanmış türev
// (Tools/Art/KeyPrompts/build_key_prompts.py → Resources/UI/KeyPrompts). Atlas bir kez yüklenir, TMP sprite varlığı çalışma
// anında bir kez kurulur ve oyun boyunca paylaşılır (yazı başına bellek ayırma yok).
// Kullanım: KeyPrompts.Apply(text); text.text = KeyPrompts.Tag(KeyPrompts.Esc) + ": geri dön".
// Atlas yoksa (ör. eksik içe aktarma) etiket düz yazıya düşer ("Esc"), yazı bozulmaz.
public static class KeyPrompts
{
    public const string Space = "space", Esc = "esc", Tab = "tab", PageUp = "pgup", PageDown = "pgdn", Enter = "enter", Alt = "alt", R = "r";
    private const string AtlasPath = "UI/KeyPrompts/KeyPromptsAtlas", LayoutPath = "UI/KeyPrompts/KeyPromptsLayout";
    // Tuş yazının büyük harf yüksekliğinden biraz iri durur; ortası küçük harflerin ortasına gelir.
    private const float IconScale = 1.7f, TopAboveBaseline = 1.25f;

    private static readonly Dictionary<string, string> Fallback = new()
    {
        { Space, "Space" }, { Esc, "Esc" }, { Tab, "Tab" }, { PageUp, "PgUp" }, { PageDown, "PgDn" }, { Enter, "Enter" }, { Alt, "Alt" }, { R, "R" }
    };
    private static readonly HashSet<string> available = new();
    private static TMP_SpriteAsset asset;
    private static bool loaded;

    [Serializable] private sealed class Layout { public int width, height; public KeyRect[] keys; }
    [Serializable] private sealed class KeyRect { public string name; public int x, y, w, h; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        asset = null;
        loaded = false;
        available.Clear();
    }

    public static TMP_SpriteAsset Asset
    {
        get
        {
            if (!loaded) { loaded = true; asset = Build(); }
            return asset;
        }
    }

    // Metindeki etiket. Aynı yazıya Apply çağrılmalıdır.
    public static string Tag(string key)
    {
        if (Asset != null && available.Contains(key)) return "<sprite name=\"" + key + "\">";
        return Fallback.TryGetValue(key, out string text) ? text : key;
    }

    public static void Apply(TMP_Text text)
    {
        if (text != null && Asset != null) text.spriteAsset = Asset;
    }

    private static TMP_SpriteAsset Build()
    {
        var texture = Resources.Load<Texture2D>(AtlasPath);
        var json = Resources.Load<TextAsset>(LayoutPath);
        if (texture == null || json == null) return null;
        Layout layout = JsonUtility.FromJson<Layout>(json.text);
        if (layout?.keys == null || layout.keys.Length == 0) return null;
        Shader shader = TMP_Settings.defaultSpriteAsset != null && TMP_Settings.defaultSpriteAsset.material != null
            ? TMP_Settings.defaultSpriteAsset.material.shader : Shader.Find("TextMeshPro/Sprite");
        if (shader == null) return null;

        var sprites = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        sprites.name = "Key Prompts (runtime)";
        sprites.hideFlags = HideFlags.DontSave;
        sprites.spriteSheet = texture;
        sprites.spriteInfoList = new List<TMP_Sprite>();
        for (int i = 0; i < layout.keys.Length; i++)
        {
            KeyRect r = layout.keys[i];
            float bearingY = r.h * TopAboveBaseline / IconScale;
            var glyph = new TMP_SpriteGlyph((uint)i, new GlyphMetrics(r.w, r.h, 0f, bearingY, r.w + 6f), new GlyphRect(r.x, r.y, r.w, r.h), IconScale, 0);
            sprites.spriteGlyphTable.Add(glyph);
            sprites.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xE000 + (uint)i, glyph) { name = r.name });
            // Eski biçim kopyası: sürüm alanı yazılamazsa TMP tabloları bundan yeniden kurar (aynı sonuç).
            sprites.spriteInfoList.Add(new TMP_Sprite
            {
                id = i, name = r.name, unicode = 0xE000 + i, x = r.x, y = r.y, width = r.w, height = r.h,
                xOffset = 0f, yOffset = bearingY, xAdvance = r.w + 6f, scale = IconScale, pivot = new Vector2(.5f, .5f)
            });
            available.Add(r.name);
        }
        // Çalışma anında kurulan varlık "eski sürüm" sanılıp yükseltilmesin (yükseltme düzenleyicide bütün varlıkları kaydeder).
        typeof(TMP_Asset).GetField("m_Version", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(sprites, "1.1.0");
        var material = new Material(shader) { name = "Key Prompts (runtime)", hideFlags = HideFlags.DontSave };
        material.mainTexture = texture;
        sprites.material = material;
        sprites.UpdateLookupTables();
        return sprites;
    }
}
