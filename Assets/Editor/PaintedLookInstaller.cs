using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// "Boyalı görünüm" kurulumu (11 Kasım 2026). Kullanıcı izole kopyada onayladı: elle boyanmış gibi karolar (3 çeşit, hafif krem),
// boyalı saksılar (5 boy) ve hafif "Hades" tam ekran geçişi ("boyama ile hafif geçiş", "3 yeterli, optimizasyona dikkat").
// Kaynaklar (kodla üretildi, AI yok): Tools/Art/HadesPrototype/paint_floor_tiles.py ve paint_planter.py (Blender + numpy).
// Bu araç yalnız Unity tarafını kurar; hepsi editörde görünür ve düzenlenir:
//  1. Doku içe aktarma: oyunda en çok 1024 (kaynaklar 2048, elle boyamak için), mip, sıkıştırma. Saksı FBX'leri: malzeme içe alınmaz.
//  2. Karo mesh'i: SM_Floor_V1'in boyamaya uygun UV'li kopyası (özgün FBX değişmez; UV kuralı paint_floor_tiles.py ile aynı).
//  3. Malzemeler: oyunun toon malzemelerinin kopyası + boyalı doku, renk çarpanı beyaz (değiştirici rengi _BaseColor ile yine çalışır).
//  4. Ground.prefab: boyalı mesh + 1. çeşit. GameScene: her karo kendi çeşidini konumundan alır (sabit karışım; çalışma anında
//     rastgelelik yok), sahneye veri olarak kaydedilir.
//  5. Saksı prefabları (5 boy): görünen mesh + malzeme boyalı. Hayalet (yerleştirme) malzemesine dokunulmaz.
//  6. ToonReferenceRenderer'a "Hades Stylize" tam ekran geçişi (değerler Hades Stylize.mat'te; saydamlardan önce: arayüz ve
//     parçacıklar etkilenmez). Oyun ve menü kamerası aynı renderer'ı kullanır.
//  7. Hasat imleci okunurluğu (HarvestCursorReadability) ve menü dioraması (karolar + saksılar boyalı menü varyantına).
// Yönetilen öğe kuralı: yalnız özgün hâlindeki şeyi değiştirir; elle değiştirilmiş olana dokunmaz; iki kez çalıştırmak çoğaltmaz.
// Toplu: Unity -batchmode -projectPath <kök> -executeMethod PaintedLookInstaller.RunBatch
public static class PaintedLookInstaller
{
    public const string Root = "Assets/Art/Painted";
    public const string TextureFolder = Root + "/Textures", MeshFolder = Root + "/Meshes", MaterialFolder = Root + "/Materials";
    public const string FloorMeshPath = MeshFolder + "/SM_Floor_V1_Boyali.asset";
    public const string HadesMaterialPath = MaterialFolder + "/Hades Stylize.mat";
    public const string GroundPrefabPath = "Assets/ProjectSettings/Ground.prefab";
    public const string GameScenePath = "Assets/Scenes/GameScene.unity";
    public const string RendererPath = "Assets/ToonReference/ToonReferenceRenderer.asset";
    public const string HadesFeatureName = "Hades Stylize";
    public const string SourceFloorFbx = "Assets/3D Assets/Main Assets/GroundFBX/SM_Floor_V1.fbx";
    public const string SourcePlanterMaterial = "Assets/Materials/GameToon/Planter_e40d91d8cb0832b44a1207d2f059956e_2100000.mat";
    public const string SourceOpenMaterial = "Assets/Materials/GameToon/Ground_bceae80c25d1ef64fa3f436304c13b04_7553114150769584927.mat";
    public const string SourceLockedMaterial = "Assets/Materials/GameToon/Locked_b3c681c61a718f341b93eaeeb9f225e8_2100000.mat";
    public static readonly string[] PlanterSizes = { "1x1", "1x2", "1x3", "2x2", "2x3" };
    public const int Variants = 3;
    public const int MaxTextureSize = 1024;
    // paint_floor_tiles.py ile aynı UV düzeni
    const float TopU0 = .004f, TopU1 = .746f, TopV0 = .254f, TopV1 = .996f, StripV0 = .02f, StripV1 = .22f;
    // Hafif geçiş (kullanıcının seçtiği): çizgi 1,6 px, gölge .5, tarama .15, kenar ışığı .35
    public const float HadesThickness = 1.6f, HadesShadow = .5f, HadesHatch = .15f, HadesRim = .35f;

    public static string OpenMaterialPath(int v) => $"{MaterialFolder}/Floor_Boyali_Acik_V{v}.mat";
    public static string LockedMaterialPath(int v) => $"{MaterialFolder}/Floor_Boyali_Kilitli_V{v}.mat";
    public static string PlanterMaterialPath(string size) => $"{MaterialFolder}/Saksi_Boyali_{size}.mat";
    public static string PlanterMeshPath(string size) => $"{MeshFolder}/Saksi_{size}_Boyali.fbx";
    public static string PlanterPrefabPath(string size) => $"Assets/Prefabs/Grass Planter {size}.prefab";

    [MenuItem("Tools/Oyun/Boyalı Görünüm: Kur - Güncelle")]
    public static void FromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
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
        var log = new List<string>();
        log.Add(ConfigureImports());
        Mesh floorMesh = EnsureFloorMesh(log);
        var open = new Material[Variants]; var locked = new Material[Variants];
        for (int v = 0; v < Variants; v++)
        {
            open[v] = EnsureMaterial(OpenMaterialPath(v), SourceOpenMaterial, $"{TextureFolder}/T_Floor_Boyali_Acik_V{v}.png");
            locked[v] = EnsureMaterial(LockedMaterialPath(v), SourceLockedMaterial, $"{TextureFolder}/T_Floor_Boyali_Kilitli_V{v}.png");
        }
        var planterMaterials = PlanterSizes.ToDictionary(s => s, s => EnsureMaterial(PlanterMaterialPath(s), SourcePlanterMaterial, $"{TextureFolder}/T_Saksi_{s}_Boyali.png"));
        AssetDatabase.SaveAssets();
        log.Add(InstallGroundPrefab(floorMesh, open[0], locked[0]));
        log.Add(InstallGameScene(floorMesh, open, locked, out Vector3 toLight));
        log.Add(InstallPlanters(planterMaterials));
        log.Add(EnsureHadesFeature(toLight));
        log.Add(HarvestCursorReadability.Apply());
        log.Add(InstallMenu(floorMesh, open, locked, planterMaterials["1x1"]));
        AssetDatabase.SaveAssets();
        return "Boyalı görünüm:\n - " + string.Join("\n - ", log);
    }

    // ---------------------------------------------------------------- içe aktarma
    static string ConfigureImports()
    {
        int textures = 0, models = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
            if (importer == null) continue;
            bool changed = importer.maxTextureSize != MaxTextureSize || !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp
                           || importer.textureCompression != TextureImporterCompression.Compressed || importer.anisoLevel != 2 || !importer.sRGBTexture;
            if (!changed) continue;
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.mipmapEnabled = true;
            importer.maxTextureSize = MaxTextureSize; importer.textureCompression = TextureImporterCompression.Compressed;
            importer.wrapMode = TextureWrapMode.Clamp; importer.anisoLevel = 2; importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport(); textures++;
        }
        foreach (string size in PlanterSizes)
        {
            var importer = AssetImporter.GetAtPath(PlanterMeshPath(size)) as ModelImporter;
            if (importer == null) throw new System.Exception("missing " + PlanterMeshPath(size));
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None && importer.importNormals == ModelImporterNormals.Import) continue;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.importNormals = ModelImporterNormals.Import;
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false; importer.isReadable = false;
            importer.SaveAndReimport(); models++;
        }
        return $"içe aktarma ayarlandı: {textures} doku (en çok {MaxTextureSize}), {models} saksı modeli";
    }

    // ---------------------------------------------------------------- karo mesh'i
    static Mesh EnsureFloorMesh(List<string> log)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(FloorMeshPath);
        if (existing != null) { log.Add("karo mesh'i var"); return existing; }
        Mesh source = AssetDatabase.LoadAllAssetsAtPath(SourceFloorFbx).OfType<Mesh>().FirstOrDefault();
        if (source == null) throw new System.Exception("SM_Floor_V1 mesh not found");
        Mesh painted = PaintedFloorMesh(source);
        AssetDatabase.CreateAsset(painted, FloorMeshPath);
        log.Add($"karo mesh'i üretildi ({painted.vertexCount} köşe)");
        return painted;
    }

    // Üst yüz tepeden düz izdüşüm; dış duvarlar alt şeritte 4 yüz yan yana. Normaller, alt mesh'ler, köşeler özgünle aynı.
    public static Mesh PaintedFloorMesh(Mesh source)
    {
        Mesh m = Object.Instantiate(source); m.name = "SM_Floor_V1_Boyali";
        Vector3[] v = m.vertices, n = m.normals; Bounds b = source.bounds;
        var uv = new Vector2[v.Length];
        float eps = Mathf.Max(b.size.x, b.size.z) * .01f;
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 p = v[i], q = n[i];
            bool outer = Mathf.Abs(q.y) < .5f && (Mathf.Abs(p.x - b.min.x) < eps || Mathf.Abs(p.x - b.max.x) < eps || Mathf.Abs(p.z - b.min.z) < eps || Mathf.Abs(p.z - b.max.z) < eps);
            if (outer)
            {
                int seg; float s;
                if (q.x > .5f) { seg = 0; s = (p.z - b.min.z) / b.size.z; }
                else if (q.x < -.5f) { seg = 1; s = (b.max.z - p.z) / b.size.z; }
                else if (q.z > .5f) { seg = 2; s = (b.max.x - p.x) / b.size.x; }
                else { seg = 3; s = (p.x - b.min.x) / b.size.x; }
                uv[i] = new Vector2((seg + .02f + .96f * s) / 4f, StripV0 + (StripV1 - StripV0) * (p.y - b.min.y) / b.size.y);
            }
            else uv[i] = new Vector2(TopU0 + (TopU1 - TopU0) * (p.x - b.min.x) / b.size.x, TopV0 + (TopV1 - TopV0) * (p.z - b.min.z) / b.size.z);
        }
        m.uv = uv;
        m.UploadMeshData(false);
        return m;
    }

    // ---------------------------------------------------------------- malzemeler
    static Material EnsureMaterial(string path, string sourcePath, string texturePath)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (source == null || texture == null) throw new System.Exception($"missing source for {path}: {sourcePath} / {texturePath}");
        var m = new Material(source) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
        m.SetTexture("_MainTex", texture);
        m.SetColor("_Color", Color.white);   // renk dokudan; oyun rengi (_BaseColor, değiştirici) yine çarpılır
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ---------------------------------------------------------------- karolar
    static bool IsOriginalFloor(Mesh mesh) => mesh != null && AssetDatabase.GetAssetPath(mesh) == SourceFloorFbx;

    static string InstallGroundPrefab(Mesh floorMesh, Material open, Material locked)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GroundPrefabPath);
        try
        {
            var cell = root.GetComponent<GroundCell>();
            var so = new SerializedObject(cell);
            var renderer = so.FindProperty("groundRenderer").objectReferenceValue as Renderer;
            var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
            if (filter == null) throw new System.Exception("Ground.prefab: groundRenderer / MeshFilter missing");
            if (!IsOriginalFloor(filter.sharedMesh)) return "Ground.prefab zaten boyalı (ya da elle değiştirilmiş): dokunulmadı";
            filter.sharedMesh = floorMesh;
            int subs = filter.sharedMesh.subMeshCount;
            so.FindProperty("lockedMaterial").objectReferenceValue = locked;
            var unlocked = so.FindProperty("unlockedMaterials");
            unlocked.arraySize = subs;   // ApplyModifier dizinin tamamını renderer'a verir: alt mesh sayısı kadar olmalı
            for (int i = 0; i < subs; i++) unlocked.GetArrayElementAtIndex(i).objectReferenceValue = open;
            so.ApplyModifiedPropertiesWithoutUndo();
            renderer.sharedMaterials = Enumerable.Repeat(locked, subs).ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, GroundPrefabPath);
            return $"Ground.prefab: boyalı mesh + 1. çeşit ({subs} alt mesh)";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Sabit karışım (Random yok): komşu karolar farklı çeşit alır. HadesPrototypeCapture.VariantFor ile aynı.
    public static int VariantFor(Vector3 position, int count)
    {
        if (count <= 1) return 0;
        int x = Mathf.RoundToInt(position.x * 4f), z = Mathf.RoundToInt(position.z * 4f);
        unchecked
        {
            uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (int)(h % (uint)count);
        }
    }

    static string InstallGameScene(Mesh floorMesh, Material[] open, Material[] locked, out Vector3 toLight)
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        var sun = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)).FirstOrDefault(l => l.type == LightType.Directional);
        toLight = sun != null ? -sun.transform.forward : new Vector3(.4f, .8f, -.3f);
        int set = 0, skipped = 0; var used = new int[Variants];
        foreach (GroundCell cell in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GroundCell>(true)))
        {
            var so = new SerializedObject(cell);
            var current = so.FindProperty("lockedMaterial").objectReferenceValue as Material;
            bool painted = locked.Contains(current);
            if (!painted) { skipped++; continue; }   // elle başka malzeme verilmiş: dokunulmaz
            int v = VariantFor(cell.transform.position, Variants);
            used[v]++;
            if (current == locked[v]) continue;
            var renderer = so.FindProperty("groundRenderer").objectReferenceValue as Renderer;
            int subs = renderer != null && renderer.GetComponent<MeshFilter>() != null ? renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount : 3;
            Undo.RecordObject(cell, "Boyalı karo çeşidi");
            so.FindProperty("lockedMaterial").objectReferenceValue = locked[v];
            var unlocked = so.FindProperty("unlockedMaterials"); unlocked.arraySize = subs;
            for (int i = 0; i < subs; i++) unlocked.GetArrayElementAtIndex(i).objectReferenceValue = open[v];
            so.ApplyModifiedProperties();
            if (renderer != null)
            {
                Undo.RecordObject(renderer, "Boyalı karo çeşidi");
                renderer.sharedMaterials = Enumerable.Repeat(locked[v], subs).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(cell);
            set++;
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return $"GameScene: {set} karoya çeşit yazıldı, dağılım [{string.Join(", ", used)}], elle değiştirilmiş {skipped}";
    }

    // ---------------------------------------------------------------- saksılar
    static string InstallPlanters(Dictionary<string, Material> materials)
    {
        var done = new List<string>();
        foreach (string size in PlanterSizes)
        {
            Mesh painted = AssetDatabase.LoadAllAssetsAtPath(PlanterMeshPath(size)).OfType<Mesh>().FirstOrDefault();
            if (painted == null) throw new System.Exception("painted planter mesh missing: " + size);
            GameObject root = PrefabUtility.LoadPrefabContents(PlanterPrefabPath(size));
            try
            {
                int swapped = 0;
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var r = filter.GetComponent<MeshRenderer>();
                    if (r == null || filter.sharedMesh == null || filter.sharedMesh.name != painted.name) continue;
                    if (AssetDatabase.GetAssetPath(filter.sharedMesh) == PlanterMeshPath(size)) continue;   // zaten boyalı
                    Material[] mats = r.sharedMaterials;
                    if (mats.Length == 0 || mats[0] == null || !mats[0].name.StartsWith("Planter_")) continue;   // hayalet malzemesi
                    filter.sharedMesh = painted;
                    r.sharedMaterials = Enumerable.Repeat(materials[size], mats.Length).ToArray();
                    swapped++;
                }
                if (swapped > 0) PrefabUtility.SaveAsPrefabAsset(root, PlanterPrefabPath(size));
                done.Add($"{size}: {swapped}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return "saksı prefabları (boy: değişen görünür parça) " + string.Join(", ", done);
    }

    // ---------------------------------------------------------------- tam ekran geçişi
    static string EnsureHadesFeature(Vector3 toLight)
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
        if (data == null) throw new System.Exception("renderer missing: " + RendererPath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(HadesMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Hidden/HadesStylize");
            if (shader == null) throw new System.Exception("Hidden/HadesStylize shader missing (Assets/Shaders/HadesStylize.shader)");
            material = new Material(shader) { name = "Hades Stylize" };
            material.SetFloat("_Thickness", HadesThickness); material.SetFloat("_ShadowStrength", HadesShadow);
            material.SetFloat("_HatchStrength", HadesHatch); material.SetFloat("_RimStrength", HadesRim);
            material.SetVector("_LightDir", toLight.normalized);
            AssetDatabase.CreateAsset(material, HadesMaterialPath);
        }
        if (data.rendererFeatures.Any(f => f != null && f.name == HadesFeatureName)) return "Hades geçişi zaten renderer'da";
        var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
        feature.name = HadesFeatureName;
        feature.passMaterial = material;
        feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;   // arayüz ve saydam parçacıklar etkilenmez
        feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Color;
        feature.fetchColorBuffer = true;
        AssetDatabase.AddObjectToAsset(feature, data);
        data.rendererFeatures.Add(feature);
        // Özellik haritası bütün özelliklerden yeniden kurulur (URP editörünün yaptığı gibi). Not: bu renderer'da harita daha önce
        // de boştu ("Simple Toon Outlines" kayıtsızdı); şimdi iki özellik de kayıtlı.
        var so = new SerializedObject(data);
        var map = so.FindProperty("m_RendererFeatureMap");
        map.arraySize = data.rendererFeatures.Count;
        for (int i = 0; i < data.rendererFeatures.Count; i++)
        {
            long id = 0;
            if (data.rendererFeatures[i] != null) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i], out string _, out id);
            map.GetArrayElementAtIndex(i).longValue = id;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        data.SetDirty(); EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        return $"Hades geçişi renderer'a eklendi (hafif: çizgi {HadesThickness}, gölge {HadesShadow}, tarama {HadesHatch}, kenar ışığı {HadesRim})";
    }

    // ---------------------------------------------------------------- menü
    static string InstallMenu(Mesh floorMesh, Material[] open, Material[] locked, Material planter1x1)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MenuDioramaBuilder.PrefabPath) == null) return "menü dioraması yok: atlandı";
        Mesh planterMesh = AssetDatabase.LoadAllAssetsAtPath(PlanterMeshPath("1x1")).OfType<Mesh>().FirstOrDefault();
        var openMenu = open.Select(MenuDioramaBuilder.MenuVariant).ToArray();
        var lockedMenu = locked.Select(MenuDioramaBuilder.MenuVariant).ToArray();
        Material planterMenu = MenuDioramaBuilder.MenuVariant(planter1x1);
        GameObject root = PrefabUtility.LoadPrefabContents(MenuDioramaBuilder.PrefabPath);
        int tiles = 0, planters = 0;
        try
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = filter.GetComponent<MeshRenderer>();
                if (r == null || filter.sharedMesh == null) continue;
                Material first = r.sharedMaterials.FirstOrDefault(m => m != null);
                if (first == null) continue;
                if (IsOriginalFloor(filter.sharedMesh) && (first.name.StartsWith("Ground_") || first.name.StartsWith("Locked_")))
                {
                    int v = VariantFor(filter.transform.position, Variants);
                    Material m = first.name.StartsWith("Locked_") ? lockedMenu[v] : openMenu[v];
                    filter.sharedMesh = floorMesh;
                    r.sharedMaterials = Enumerable.Repeat(m, floorMesh.subMeshCount).ToArray();
                    tiles++;
                }
                else if (planterMesh != null && filter.sharedMesh.name == planterMesh.name && filter.sharedMesh != planterMesh && first.name.StartsWith("Planter_"))
                {
                    filter.sharedMesh = planterMesh;
                    r.sharedMaterials = Enumerable.Repeat(planterMenu, r.sharedMaterials.Length).ToArray();
                    planters++;
                }
            }
            if (tiles + planters > 0) PrefabUtility.SaveAsPrefabAsset(root, MenuDioramaBuilder.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return $"menü dioraması: {tiles} karo, {planters} saksı boyalı menü varyantına geçti";
    }
}
