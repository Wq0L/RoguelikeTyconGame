using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Builds a reproducible Simple Toon reference without editing GameScene.</summary>
public static class SimpleToonUrpSetup
{
    const string Root = "Assets/ToonReference";
    const string ScenePath = Root + "/SimpleToon_URP_Test.unity";
    const string FeatureName = "Simple Toon Outlines";

    [MenuItem("Tools/Simple Toon/Create and Open URP Test")]
    public static void CreateAndOpen()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    [MenuItem("Tools/Simple Toon/Enable Outlines on Project Renderers")]
    public static void EnableOutlines()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" }))
            AddOutline(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid)));
        AssetDatabase.SaveAssets();
    }

    static void AddOutline(UniversalRendererData renderer)
    {
        if (renderer.rendererFeatures.Any(f => f != null && f.name == FeatureName)) return;
        var feature = ScriptableObject.CreateInstance<RenderObjects>();
        feature.name = FeatureName;
        feature.settings.Event = RenderPassEvent.AfterRenderingOpaques;
        feature.settings.filterSettings.LayerMask = -1;
        feature.settings.filterSettings.PassNames = new[] { "SimpleToonOutline" };
        feature.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None;
        AssetDatabase.AddObjectToAsset(feature, renderer);
        renderer.rendererFeatures.Add(feature);
        renderer.SetDirty();
        EditorUtility.SetDirty(renderer);
    }

    static int RegisterRenderer(UniversalRenderPipelineAsset pipeline, UniversalRendererData renderer)
    {
        var serialized = new SerializedObject(pipeline);
        var list = serialized.FindProperty("m_RendererDataList");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer) return i;
        int index = list.arraySize++;
        list.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
        return index;
    }

    static Camera Build()
    {
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "ToonReference");
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Root + "/ToonReferenceRenderer.asset");
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, Root + "/ToonReferenceRenderer.asset");
        }
        AddOutline(renderer);
        EnableOutlines();
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null) throw new InvalidOperationException("An active URP pipeline is required.");
        int rendererIndex = RegisterRenderer(pipeline, renderer);
        // Quality tiers must resolve the camera's renderer index to the same reference renderer.
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets/Settings" }))
        {
            var other = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (RegisterRenderer(other, renderer) != rendererIndex)
                Debug.LogWarning("Simple Toon test renderer index differs for " + other.name);
        }
        if (!File.Exists(ScenePath))
            AssetDatabase.CopyAsset("Assets/3D Assets/Simple Toon/Demo/Scenes/DemoScene2.unity", ScenePath);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.28f, 0.89f, 0.43f);
        camera.allowHDR = false;
        camera.fieldOfView = 50;
        var data = camera.GetUniversalAdditionalCameraData();
        data.SetRenderer(rendererIndex);
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        data.renderShadows = true;
        var background = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Background");
        if (background != null)
        {
            const string materialPath = Root + "/ReferenceGreen.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Simple Toon/SToon Default"));
                material.SetColor("_Color", new Color(75f / 255, 228f / 255, 111f / 255));
                material.SetFloat("_Offset", 1.1f);
                material.SetFloat("_Steps", 1);
                material.SetFloat("_MinLight", 0);
                material.SetFloat("_MaxLight", 1);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            background.GetComponent<Renderer>().sharedMaterial = material;
        }
        foreach (var light in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)))
        {
            // The asset's original point light is unused by the main-light reference setup.
            light.enabled = light.type == LightType.Directional;
            if (light.enabled) { light.shadows = LightShadows.Hard; light.shadowStrength = 1; light.shadowBias = 0.03f; light.shadowNormalBias = 0.05f; }
        }
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeObject = camera.gameObject;
        Debug.Log("Simple Toon URP reference ready: " + ScenePath);
        return camera;
    }

    // Invoked by a separate validation editor; never runs automatically in the user's editor.
    public static void ValidateBatch()
    {
        try
        {
            var camera = Build();
            int frames = 0;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (++frames < 30) { EditorApplication.QueuePlayerLoopUpdate(); return; }
                EditorApplication.update -= tick;
                try { Capture(camera); EditorApplication.Exit(0); }
                catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
            };
            EditorApplication.update += tick;
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    [MenuItem("Tools/Simple Toon/Capture Active Test Camera")]
    public static void CaptureActive() { Capture(Camera.main); }

    static void Capture(Camera camera)
    {
        if (camera == null) throw new InvalidOperationException("No main camera.");
        Directory.CreateDirectory("Logs/ToonReference");
        var target = new RenderTexture(2560, 1440, 24, RenderTextureFormat.ARGB32);
        var old = RenderTexture.active;
        Texture2D image = null;
        try
        {
            target.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes("Logs/ToonReference/simple-toon-urp.png", image.EncodeToPNG());
            foreach (string name in new[] { "SToon Default", "SToon Outline", "SToon Transparent" })
            {
                var shader = Shader.Find("Simple Toon/" + name);
                if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
                    throw new InvalidOperationException("Shader validation failed: " + name);
            }
            int pink = image.GetPixels32().Count(p => p.r > 220 && p.b > 220 && p.g < 40);
            if (pink > 100) throw new InvalidOperationException("Possible error-shader pixels: " + pink);
            File.WriteAllText("Logs/ToonReference/validation.txt", "PASS: URP render, three supported shaders, no shader errors; magenta pixels=" + pink);
            Debug.Log("SIMPLE_TOON_URP_VALIDATION_PASS");
        }
        finally
        {
            RenderTexture.active = old;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
