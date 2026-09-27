using System.IO;
using UnityEditor;

[InitializeOnLoad]
public static class PerformanceTrendCaptureMenu
{
    static PerformanceTrendCaptureMenu()
    {
        AssemblyReloadEvents.beforeAssemblyReload += PerformanceTrendCapture.Stop;
        EditorApplication.playModeStateChanged += _ => { if (!EditorApplication.isPlaying) PerformanceTrendCapture.Stop(); };
    }
    [MenuItem("Tools/Performance/Start Trend Capture (Play Mode)")]
    static void Start() => PerformanceTrendCapture.Begin();
    [MenuItem("Tools/Performance/Start Trend Capture (Play Mode)", true)]
    static bool CanStart() => EditorApplication.isPlaying && !PerformanceTrendCapture.IsRecording;
    [MenuItem("Tools/Performance/Stop Trend Capture")]
    static void Stop() => PerformanceTrendCapture.Stop();
    [MenuItem("Tools/Performance/Stop Trend Capture", true)]
    static bool CanStop() => PerformanceTrendCapture.IsRecording;
    [MenuItem("Tools/Performance/Open Capture Folder")]
    static void Open()
    {
        Directory.CreateDirectory("Logs/Performance");
        EditorUtility.RevealInFinder(Path.GetFullPath("Logs/Performance"));
    }
}
