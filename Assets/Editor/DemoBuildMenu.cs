using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class DemoBuildMenu
{
    private const string Scene = "Assets/Scenes/DemoScene.unity";

    public static void BuildBatch()
    {
        try
        {
            string destination = System.Environment.GetEnvironmentVariable("CLICKER_DEMO_OUTPUT");
            if (string.IsNullOrEmpty(destination)) throw new System.InvalidOperationException("CLICKER_DEMO_OUTPUT is required.");
            Build(destination);
            EditorApplication.Exit(0);
        }
        catch (System.Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("Tools/Demo/Build WebGL Demo")]
    public static void BuildWebGL()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            EditorUtility.DisplayDialog("WebGL Demo", "Unity Hub üzerinden bu Unity sürümüne WebGL Build Support ekleyin.", "Tamam");
            return;
        }
        string destination = EditorUtility.SaveFolderPanel("WebGL demo çıktı klasörü", "Builds", "WebGLDemo");
        if (string.IsNullOrEmpty(destination)) return;
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build(destination);
    }

    // Scene list is supplied per build; the full game's shared Build Settings remain unchanged.
    public static BuildReport Build(string destination)
    {
        bool fallback = PlayerSettings.WebGL.decompressionFallback;
        int previousQuality = QualitySettings.GetQualityLevel();
        var pipelines = new RenderPipelineAsset[QualitySettings.names.Length];
        var demoPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/DemoWebGL_RPAsset.asset");
        if (demoPipeline == null) throw new System.InvalidOperationException("Demo WebGL render pipeline is missing.");
        for (int i = 0; i < pipelines.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            pipelines[i] = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = demoPipeline;
        }
        QualitySettings.SetQualityLevel(previousQuality, false);
        try
        {
            PlayerSettings.WebGL.decompressionFallback = true;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { Scene }, target = BuildTarget.WebGL,
                locationPathName = Path.GetFullPath(destination), options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException("WebGL demo build failed: " + report.summary.result);
            PrepareWebPage(destination);
            Debug.Log("WebGL demo hazır: " + destination + ". index.html, Build ve TemplateData içeriğini birlikte ZIP yapın.");
            return report;
        }
        finally
        {
            PlayerSettings.WebGL.decompressionFallback = fallback;
            for (int i = 0; i < pipelines.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipelines[i];
            }
            QualitySettings.SetQualityLevel(previousQuality, false);
        }
    }

    private static void PrepareWebPage(string destination)
    {
        string path = Path.Combine(destination, "index.html");
        string html = File.ReadAllText(path);
        html = html.Replace("<title>Unity Web Player | " + PlayerSettings.productName + "</title>",
            "<title>Earth Is Gone. Lunch Isn't. - Demo</title>");
        html = html.Replace("<div id=\"unity-build-title\">" + PlayerSettings.productName + "</div>",
            "<div id=\"unity-build-title\">Earth Is Gone. Lunch Isn't.</div>");
        File.WriteAllText(path, html);
        // Fit itch.io's iframe and avoid stretching a small fixed canvas on desktop.
        File.AppendAllText(Path.Combine(destination, "TemplateData/style.css"),
            "\nhtml,body{width:100%;height:100%;overflow:hidden;background:#181d2b;}" +
            "\n#unity-container.unity-desktop{position:fixed;inset:0;width:100%;height:100%;transform:none;}" +
            "\n#unity-container.unity-desktop #unity-canvas{display:block;width:100%!important;height:calc(100% - 38px)!important;}" +
            "\n#unity-footer{height:38px;background:#181d2b;color:white;}\n");
    }
}
