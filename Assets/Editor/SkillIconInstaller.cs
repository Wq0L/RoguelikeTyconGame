using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Yetenek ikonlarını bağlar (12 Kasım 2026; kullanıcı 19 ikonu Assets/Art/SkillIcons'a koydu, "A seçeneği").
//  1. İçe aktarma: Sprite (tek), saydamlık, mip, en çok 256 px (düğümde ~70 px görünür), sıkıştırılmış, kenar sabit.
//  2. Assets/Resources/SkillIconSet.asset: SkillIconCatalog'un bütün kimlikleri; dosya adı kimlikle aynıysa sprite bağlanır.
// Yönetilen öğe kuralı: sette elle seçilmiş sprite'a dokunmaz (yalnız boş kayıtları doldurur, eksik kimliği ekler); tekrar çalıştırmak
// çoğaltmaz. Yetenek verisine dokunmaz. Toplu: Unity -batchmode -projectPath <kök> -executeMethod SkillIconInstaller.RunBatch
public static class SkillIconInstaller
{
    public const string Folder = "Assets/Art/SkillIcons";
    public const string SetPath = "Assets/Resources/SkillIconSet.asset";
    public const int MaxSize = 256;
    // SkillIconCatalog'daki bütün kimlikler (time ve unlock bu ağaçta kullanılmıyor; dosyası yoksa boş kalır, eski simge çizilir).
    public static readonly string[] Ids =
    {
        "attack", "speed", "area", "critical", "spawn", "rare", "gold", "iron", "stone", "xp", "score", "time", "grid", "planter",
        "cards", "explosion", "duplicate", "tornado", "scythe", "electric", "unlock"
    };

    [MenuItem("Tools/Oyun/Yetenek İkonları: Bağla - Güncelle")]
    public static void FromMenu()
    {
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;   // sahneler açılıp kaydedilir
        Debug.Log(Install());
    }

    public static void RunBatch()
    {
        int code = 0;
        try { Debug.Log(Install()); }
        catch (System.Exception e) { Debug.LogException(e); code = 1; }
        EditorApplication.Exit(code);
    }

    public static string Install()
    {
        int configured = 0;
        foreach (string path in Directory.GetFiles(Folder, "*.png").Select(p => p.Replace('\\', '/')))
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            bool ok = importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single && importer.alphaIsTransparency
                      && importer.mipmapEnabled && importer.maxTextureSize == MaxSize && importer.wrapMode == TextureWrapMode.Clamp;
            if (ok) continue;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.sRGBTexture = true; importer.mipmapEnabled = true;
            importer.maxTextureSize = MaxSize; importer.textureCompression = TextureImporterCompression.Compressed;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport(); configured++;
        }

        var set = AssetDatabase.LoadAssetAtPath<SkillIconSetSO>(SetPath);
        bool created = set == null;
        if (created)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            set = ScriptableObject.CreateInstance<SkillIconSetSO>();
            AssetDatabase.CreateAsset(set, SetPath);
        }
        int added = 0, linked = 0;
        foreach (string id in Ids)
        {
            int index = set.entries.FindIndex(e => e.id == id);
            if (index < 0) { set.entries.Add(new SkillIconSetSO.Entry { id = id }); index = set.entries.Count - 1; added++; }
            if (set.entries[index].sprite != null) continue;   // elle seçilmiş ya da önceden bağlı: dokunma
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{id}.png");
            if (sprite == null) continue;
            set.entries[index] = new SkillIconSetSO.Entry { id = id, sprite = sprite };
            linked++;
        }
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        int withArt = set.entries.Count(e => e.sprite != null);
        return $"Yetenek ikonları: {configured} doku ayarlandı (Sprite, en çok {MaxSize}), set {(created ? "oluşturuldu" : "vardı")}, " +
               $"{added} kimlik eklendi, {linked} sprite bağlandı; sette {withArt}/{set.entries.Count} ikon çizili; " + ApplyResourceIcons();
    }

    // Kaynak ikonları (12 Kasım 2026; kullanıcı: "buradaki gold, stone, iron'u bizim resource image'imizde de kullan"): temanın altın /
    // demir / taş sprite'ı (HUD, saksı dükkânı, round özeti buradan okur) ve sahnelerde eski altın sprite'ını doğrudan gösteren görseller
    // yeni çizime geçer. Yalnız ESKİ sprite'ı gösteren yerler değişir (elle seçilen başka sprite'a dokunulmaz); eski sprite'lar silinmez.
    public const string ThemePath = "Assets/Resources/ComicUITheme.asset";
    public static readonly string[] OldResourceSprites =
        { "Assets/Art/UI/ComicToon/Coin Sprite.asset", "Assets/Art/UI/ComicToon/Iron Sprite.asset", "Assets/Art/UI/ComicToon/Stone Sprite.asset" };
    public static readonly string[] ResourceScenes = { "Assets/Scenes/GameScene.unity", "Assets/Scenes/DemoScene.unity" };

    public static string ApplyResourceIcons()
    {
        var gold = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/gold.png");
        var iron = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/iron.png");
        var stone = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/stone.png");
        var theme = AssetDatabase.LoadAssetAtPath<ComicUITheme>(ThemePath);
        if (gold == null || iron == null || stone == null || theme == null) return "kaynak ikonları: dosya / tema yok, atlandı";
        var old = OldResourceSprites.Select(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>().FirstOrDefault()).ToArray();
        int themeSwaps = 0;
        if (theme.coinSprite == null || theme.coinSprite == old[0]) { theme.coinSprite = gold; themeSwaps++; }
        if (theme.ironSprite == null || theme.ironSprite == old[1]) { theme.ironSprite = iron; themeSwaps++; }
        if (theme.stoneSprite == null || theme.stoneSprite == old[2]) { theme.stoneSprite = stone; themeSwaps++; }
        if (themeSwaps > 0) { EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets(); }

        int sceneSwaps = 0;
        var replacements = new System.Collections.Generic.Dictionary<Sprite, Sprite>();
        if (old[0] != null) replacements[old[0]] = gold;
        if (old[1] != null) replacements[old[1]] = iron;
        if (old[2] != null) replacements[old[2]] = stone;
        string active = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        foreach (string path in ResourceScenes)
        {
            if (!File.Exists(path)) continue;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
            int here = 0;
            foreach (var image in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UnityEngine.UI.Image>(true)))
            {
                if (image.sprite == null || !replacements.TryGetValue(image.sprite, out Sprite next)) continue;
                Undo.RecordObject(image, "Yeni kaynak ikonu");
                image.sprite = next; image.preserveAspect = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(image);
                here++;
            }
            if (here > 0) { UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene); }
            sceneSwaps += here;
        }
        if (!string.IsNullOrEmpty(active) && File.Exists(active)) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(active);
        return $"kaynak ikonları: temada {themeSwaps} sprite, sahnelerde {sceneSwaps} görsel yeni çizime geçti";
    }
}
