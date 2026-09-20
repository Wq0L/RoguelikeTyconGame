using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class GameToonMigration
{
    const string Root = "Assets/Materials/GameToon";
    const string ScenePath = "Assets/Scenes/GameScene.unity";
    static readonly HashSet<string> changed = new HashSet<string>();
    static readonly Dictionary<string, Material> converted = new Dictionary<string, Material>();

    [MenuItem("Tools/Simple Toon/Apply to GameScene")]
    public static void ApplyMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Apply();
    }

    static Material Convert(Material source, string kind)
    {
        if (source == null) throw new InvalidOperationException("Missing source material: " + kind);
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (sourcePath.StartsWith(Root + "/"))
        {
            if (kind == "Locked") ConfigureLockedMaterial(source);
            return source;
        }
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id);
        string key = kind + "_" + guid + "_" + id.ToString().Replace('-', 'n');
        if (converted.TryGetValue(key, out var cached)) return cached;
        string path = Root + "/" + key + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            bool ground = kind == "Ground" || kind == "Locked";
            material = new Material(Shader.Find(ground ? "Simple Toon/SToon Default" : "Simple Toon/SToon Outline"));
            material.name = source.name + " Toon " + kind;
            Color tint = Color.white;
            foreach (string property in new[] { "_BaseColor", "_Color" })
                if (source.HasProperty(property)) { tint = source.GetColor(property); break; }
            material.SetColor("_Color", tint);
            material.SetColor("_BaseColor", Color.white);
            foreach (string property in new[] { "_BaseMap", "_BaseColorMap", "_MainTex", "_MainTexure" })
            {
                if (!source.HasProperty(property) || source.GetTexture(property) == null) continue;
                material.SetTexture("_MainTex", source.GetTexture(property));
                material.SetTextureScale("_MainTex", source.GetTextureScale(property));
                material.SetTextureOffset("_MainTex", source.GetTextureOffset(property));
                break;
            }
            material.SetFloat("_Segmented", 1);
            material.SetFloat("_Steps", ground ? 2 : 3);
            material.SetFloat("_StpSmooth", 0);
            material.SetFloat("_Offset", ground ? 0.3f : 0.02f);
            material.SetFloat("_MinLight", ground ? 0.25f : 0.351f);
            material.SetFloat("_MaxLight", 1);
            material.SetFloat("_OtlWidth", 0);
            material.SetFloat("_OtlWorldWidth", ground ? 0 : kind == "Plant" ? 0.012f : 0.018f);
            material.SetColor("_OtlColor", new Color(0.065f, 0.08f, 0.035f));
            material.SetFloat("_ShnIntense", ground ? 0 : 0.553f);
            material.SetFloat("_ShnRange", 0.052f);
            material.SetFloat("_ShnSmooth", 0);
            material.SetColor("_ShnColor", new Color(1, 0.96f, 0.8f));
            material.SetFloat("_Cull", source.HasProperty("_Cull") ? source.GetFloat("_Cull") : 2);
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_OtlWorldWidth", kind == "Ground" || kind == "Locked" ? 0 : kind == "Plant" ? 0.012f : 0.018f);
        material.SetFloat("_Cull", source.HasProperty("_Cull") ? source.GetFloat("_Cull") : 2);
        if (kind == "Locked") ConfigureLockedMaterial(material);
        EditorUtility.SetDirty(material);
        converted[key] = material;
        return material;
    }

    static void ConfigureLockedMaterial(Material material)
    {
        // Muted blue charcoal keeps locked tiles readable without resembling open tiles.
        material.SetColor("_Color", new Color(0.22f, 0.29f, 0.38f, 1));
        material.SetFloat("_Steps", 3);
        material.SetFloat("_MinLight", 0.45f);
        material.SetFloat("_MaxLight", 0.85f);
        material.SetFloat("_Offset", 0.15f);
        material.SetFloat("_AmbientCol", 0);
        material.SetFloat("_ColBright", 0);
        material.SetFloat("_ShnIntense", 0);
        EditorUtility.SetDirty(material);
    }

    static bool IsGhost(Transform transform)
    {
        for (; transform != null; transform = transform.parent)
            if (transform.name.IndexOf("Ghost", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    static void ConvertRoot(GameObject root, string kind)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            if (IsGhost(renderer.transform)) continue;
            string rendererKind = renderer.GetComponentInParent<GroundCell>() != null ? "Ground" : kind;
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Convert(m, rendererKind)).ToArray();
            renderer.receiveShadows = true;
            if (rendererKind != "Ground") renderer.shadowCastingMode = ShadowCastingMode.On;
            EditorUtility.SetDirty(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        foreach (var cell in root.GetComponentsInChildren<GroundCell>(true))
        {
            var data = new SerializedObject(cell);
            var unlocked = data.FindProperty("unlockedMaterials");
            for (int i = 0; i < unlocked.arraySize; i++)
            {
                var slot = unlocked.GetArrayElementAtIndex(i);
                slot.objectReferenceValue = Convert(slot.objectReferenceValue as Material, "Ground");
            }
            var locked = data.FindProperty("lockedMaterial");
            locked.objectReferenceValue = Convert(locked.objectReferenceValue as Material, "Locked");
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(cell);
        }
    }

    static Camera Apply()
    {
        changed.Clear(); converted.Clear();
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/Materials", "GameToon");
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Plants" })
            .Select(AssetDatabase.GUIDToAssetPath).Concat(Directory.GetFiles("Assets/Prefabs", "Grass Planter*.prefab"))
            .Concat(new[] { "Assets/Prefabs/Ground.prefab" });
        foreach (string path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConvertRoot(root, path.Replace('\\', '/').Contains("/Plants/") ? "Plant" : "Planter");
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed.Add(path.Replace('\\', '/'));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects()) ConvertRoot(root, "Environment");
        var camera = Camera.main;
        if (camera == null) throw new InvalidOperationException("GameScene main camera missing.");
        // Reuse the verified toon-only renderer, retaining gameplay camera framing/projection.
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/ToonReference/ToonReferenceRenderer.asset");
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        var serialized = new SerializedObject(pipeline);
        var list = serialized.FindProperty("m_RendererDataList");
        int index = -1;
        for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer) index = i;
        if (index < 0) throw new InvalidOperationException("Run Simple Toon URP setup first.");
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.SetRenderer(index);
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = true;
        camera.allowHDR = false;
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional) continue;
            light.color = Color.white; light.intensity = 1; light.useColorTemperature = false;
            light.shadows = LightShadows.Hard; light.shadowStrength = 1;
            light.shadowBias = 0.03f; light.shadowNormalBias = 0.08f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
        }
        EditorSceneManager.SaveScene(scene);
        changed.Add(ScenePath);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs/GameToon");
        File.WriteAllLines("Logs/GameToon/changed-files.txt", changed.OrderBy(p => p));
        Debug.Log("GAME_TOON_APPLIED: " + converted.Count + " material variants, " + changed.Count + " assets.");
        return camera;
    }

    public static void ApplyAndValidateBatch()
    {
        try
        {
            var camera = Apply();
            AddPreviewSamples();
            int frames = 0;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (++frames < 30) { EditorApplication.QueuePlayerLoopUpdate(); return; }
                EditorApplication.update -= tick;
                try
                {
                    Capture(camera);
                    // Detail view is an unsaved render; keep GameScene's playable framing intact.
                    var cells = Object.FindObjectsByType<GroundCell>(FindObjectsSortMode.None);
                    Vector3 center = cells.Aggregate(Vector3.zero, (sum, c) => sum + c.transform.position) / cells.Length;
                    camera.transform.position = center + Vector3.up * 0.3f - camera.transform.forward * 20;
                    camera.orthographicSize = 4.2f;
                    Capture(camera, "gamescene-toon-detail.png");
                    Verify(); EditorApplication.Exit(0);
                }
                catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
            };
            EditorApplication.update += tick;
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void AddPreviewSamples()
    {
        // Unsaved editor-only samples expose all plant silhouettes at gameplay scale.
        var cells = Object.FindObjectsByType<GroundCell>(FindObjectsSortMode.None);
        Vector3 center = cells.Aggregate(Vector3.zero, (sum, c) => sum + c.transform.position) / cells.Length;
        var selected = cells.OrderBy(c => (c.transform.position - center).sqrMagnitude).Take(12).ToArray();
        var plants = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Plants" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
        for (int i = 0; i < selected.Length; i++)
        {
            var cell = selected[i];
            var pot = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Grass Planter 1x1.prefab")) as GameObject;
            pot.transform.position = cell.transform.position;
            var plant = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(plants[i % plants.Length])) as GameObject;
            plant.transform.position = cell.transform.position + Vector3.up * 0.3f;
        }
    }

    public static void Capture(Camera camera, string filename = "gamescene-toon.png")
    {
        var target = new RenderTexture(2560, 1440, 24, RenderTextureFormat.ARGB32);
        var old = RenderTexture.active;
        Texture2D image = null;
        try
        {
            target.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes("Logs/GameToon/" + filename, image.EncodeToPNG());
            int pink = image.GetPixels32().Count(p => p.r > 240 && p.b > 240 && p.g < 15);
            if (pink > 100) throw new InvalidOperationException("Possible error shader: " + pink + " magenta pixels.");
        }
        finally { RenderTexture.active = old; if (image != null) Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target); }
    }

    static void Verify()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat.shader == null || !mat.shader.isSupported || ShaderUtil.ShaderHasError(mat.shader))
                throw new InvalidOperationException("Invalid toon material " + mat.name);
            if (!mat.HasProperty("_BaseColor")) throw new InvalidOperationException("Missing gameplay tint " + mat.name);
        }
        var pot = Object.FindFirstObjectByType<GhostController>();
        var renderers = pot.GetComponentsInChildren<MeshRenderer>().ToArray();
        var original = renderers.Select(r => r.sharedMaterials).ToArray();
        pot.SetGhostMode(true); pot.SetColor(true); pot.SetGhostMode(false);
        for (int i = 0; i < renderers.Length; i++)
            if (!original[i].SequenceEqual(renderers[i].sharedMaterials)) throw new InvalidOperationException("Ghost material restoration failed.");
        File.WriteAllText("Logs/GameToon/validation.txt", "PASS: game scripts compile; GameScene camera rendered; toon materials supported and error-free; _BaseColor gameplay tint available; placement ghost restores toon materials. Preview plants are unsaved samples, not a saved gameplay layout.");
        Debug.Log("GAME_TOON_VALIDATION_PASS");
    }
}
