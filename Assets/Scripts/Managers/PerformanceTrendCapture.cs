using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using DG.Tweening;
using UnityEngine;

// Opt-in runtime diagnostic for normal EXE builds: press F9 in-game to start/stop a CSV capture.
// Zero footprint until then: UIManager polls F9, and the capture object is created on the first press
// inside the game scene (no startup object, no DontDestroyOnLoad), so it never survives a scene load.
public sealed class PerformanceTrendCapture : MonoBehaviour
{
    const double WindowSeconds = 5;
    static StreamWriter writer;
    static double began, last;
    static int frame, logs, warnings, errors;
    static string path, systemColumns;
    static bool beganInMenu;
    static readonly StringBuilder row = new(1024);
    // Per-window frame times, so single-frame drops are not hidden by the 5 s average.
    static readonly float[] frameTimes = new float[65536];
    static int windowFrames, over16, over33;
    static float maxFrameMs;
    public static bool IsRecording => writer != null;
    public static string LastPath => path;

    static PerformanceTrendCapture probe;
    PerformanceCaptureIndicator indicator;

    static bool Paused
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.EditorApplication.isPaused;
#else
            return false;
#endif
        }
    }
    static bool InMainMenu => GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.MainMenu;

    static void EnsureProbe()
    {
        if (probe != null) return;
        // Lives in the current scene: unloading it destroys the probe and closes the file.
        var go = new GameObject("Performance Capture (F9)");
        probe = go.AddComponent<PerformanceTrendCapture>();
        probe.indicator = go.AddComponent<PerformanceCaptureIndicator>();
        probe.indicator.enabled = false;
    }

    void Update()
    {
        if (IsRecording)
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            if (windowFrames < frameTimes.Length) frameTimes[windowFrames++] = ms;
            if (ms > maxFrameMs) maxFrameMs = ms;
            if (ms > 16.7f) over16++;
            if (ms > 33.3f) over33++;
        }
        if (!IsRecording) return;
        // Returning to the menu ends the session before the next scene load.
        if (!beganInMenu && InMainMenu) { Close(true); Notify("PERF KAYIT DURDU (ana menü)"); return; }
        Tick();
    }
    void OnApplicationQuit() => Stop();
    void OnDestroy() { if (probe == this) { Stop(); probe = null; } }

    // Called from UIManager's existing key polling (game scene only).
    public static void ToggleFromHotkey()
    {
        if (IsRecording) { Close(true); Notify("PERF KAYIT DURDU"); return; }
        if (InMainMenu) return;
        Begin();
    }

    static void Notify(string message)
    {
        if (probe == null || probe.indicator == null) return;
        probe.indicator.Show(message);
    }

    public static void Begin()
    {
        if (!Application.isPlaying || IsRecording) return;
        EnsureProbe();
        string folder = Application.isEditor ? Path.GetFullPath("Logs/Performance") : Path.Combine(Application.persistentDataPath, "Performance");
        Directory.CreateDirectory(folder);
        path = Path.Combine(folder, "trend-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".csv");
        writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("elapsed_s,window_s,frames,avg_fps,avg_frame_ms,frame_ms_p99,frame_ms_max,frames_over_16ms,frames_over_33ms," +
            "state,round,time_scale,focused,editor_paused,text_total,text_leased,hit_total,hit_leased,explosion_total,explosion_leased," +
            "plant_created,plant_active,plant_inactive,plant_pending,tweens_active,tweens_playing,global_modifiers,managed_heap_bytes," +
            "gc0_total,logs,warnings,errors,screen,fullscreen_mode,quality,vsync,target_fps,graphics_api,gpu,cpu,ram_mb,probe_ms,recorder_version");
        writer.Flush();
        systemColumns = Clean(SystemInfo.graphicsDeviceType.ToString()) + "," + Clean(SystemInfo.graphicsDeviceName) + "," +
            Clean(SystemInfo.processorType) + "," + SystemInfo.systemMemorySize.ToString(CultureInfo.InvariantCulture);
        began = last = Time.realtimeSinceStartupAsDouble;
        frame = Time.frameCount;
        logs = warnings = errors = 0;
        ResetWindow();
        beganInMenu = InMainMenu;
        probe.indicator.SetRecording(true);
        Notify("PERF KAYIT BAŞLADI");
        UnityEngine.Debug.Log("Performance capture v3 (F9): " + path);
        Application.logMessageReceived += CountLog;
    }

    // Flush only: do not query Unity objects while the Player is shutting down.
    public static void Stop() => Close(false);

    static void Close(bool sampleLast)
    {
        if (!IsRecording) return;
        try
        {
            if (sampleLast) Sample();
            writer.Flush();
        }
        finally
        {
            Application.logMessageReceived -= CountLog;
            writer.Dispose(); writer = null;
            if (probe != null && probe.indicator != null) probe.indicator.SetRecording(false);
        }
    }

    static void CountLog(string message, string stack, LogType type)
    {
        if (type == LogType.Log) logs++;
        else if (type == LogType.Warning) warnings++;
        else errors++;
    }

    static void Tick()
    {
        if (!IsRecording || !Application.isPlaying || Time.realtimeSinceStartupAsDouble - last < WindowSeconds) return;
        try { Sample(); }
        catch (Exception ex)
        {
            Application.logMessageReceived -= CountLog;
            writer.Dispose(); writer = null;
            if (probe != null && probe.indicator != null) probe.indicator.SetRecording(false);
            UnityEngine.Debug.LogException(ex);
        }
    }

    static void Add(object value)
    {
        if (row.Length > 0) row.Append(',');
        row.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    static string Clean(string value) => string.IsNullOrEmpty(value) ? "unknown" : value.Replace(',', ';').Replace('\n', ' ').Replace('\r', ' ');

    static void ResetWindow()
    {
        windowFrames = over16 = over33 = 0;
        maxFrameMs = 0f;
    }

    static float Percentile99()
    {
        if (windowFrames == 0) return -1f;
        Array.Sort(frameTimes, 0, windowFrames);
        int index = Math.Min(windowFrames - 1, Math.Max(0, (int)Math.Ceiling(windowFrames * .99) - 1));
        return frameTimes[index];
    }

    public static void Sample()
    {
        if (!IsRecording) return;
        double now = Time.realtimeSinceStartupAsDouble, seconds = now - last;
        if (seconds <= .001) return;
        long start = Stopwatch.GetTimestamp();
        int frames = Math.Max(0, Time.frameCount - frame);
        row.Clear();
        Add(Math.Round(now - began, 3)); Add(Math.Round(seconds, 3)); Add(frames);
        Add(Math.Round(frames / seconds, 2)); Add(Math.Round(frames > 0 ? seconds * 1000 / frames : -1, 3));
        Add(Math.Round(Percentile99(), 3)); Add(Math.Round(maxFrameMs, 3)); Add(over16); Add(over33);
        Add(GameManager.Instance != null ? GameManager.Instance.CurrentState.ToString() : "none");
        Add(RoundManager.Instance != null ? RoundManager.Instance.CurrentRound : 0);
        Add(Time.timeScale); Add(Application.isFocused ? 1 : 0); Add(Paused ? 1 : 0);
        var vfx = VFXManager.Instance;
        Add(vfx != null ? vfx.TextCreated : 0); Add(vfx != null ? vfx.TextLeased : 0);
        Add(vfx != null ? vfx.HitCreated : 0); Add(vfx != null ? vfx.HitLeased : 0);
        Add(vfx != null ? vfx.ExplosionCreated : 0); Add(vfx != null ? vfx.ExplosionLeased : 0);
        PlantPool.ReadTotals(out int created, out int activePlants, out int inactivePlants, out int pending);
        Add(created); Add(activePlants); Add(inactivePlants); Add(pending);
        Add(DOTween.TotalActiveTweens()); Add(DOTween.TotalPlayingTweens());
        Add(StatManager.Instance != null ? StatManager.Instance.GlobalModifiers.Count : 0);
        Add(GC.GetTotalMemory(false));
        Add(GC.CollectionCount(0)); Add(logs); Add(warnings); Add(errors);
        Add(Screen.width + "x" + Screen.height); Add(Screen.fullScreenMode);
        Add(Clean(QualitySettings.names[QualitySettings.GetQualityLevel()]));
        Add(QualitySettings.vSyncCount); Add(Application.targetFrameRate);
        row.Append(',').Append(systemColumns);
        Add(Math.Round((Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency, 4));
        Add(3); // Recorder schema version: F9 toggle, drop columns, hardware columns.
        writer.WriteLine(row.ToString()); writer.Flush();
        last = now; frame = Time.frameCount;
        ResetWindow();
    }
}

// Small on-screen REC label; disabled (no OnGUI cost) unless recording or showing a message.
public sealed class PerformanceCaptureIndicator : MonoBehaviour
{
    string message, label;
    float hideAt, recordingSince;
    int shownSecond = -1;
    bool recording;
    GUIStyle style;

    public void SetRecording(bool value)
    {
        recording = value;
        recordingSince = Time.unscaledTime;
        shownSecond = -1;
        enabled = recording || Time.unscaledTime < hideAt;
    }

    public void Show(string text)
    {
        message = text;
        hideAt = Time.unscaledTime + 2.5f;
        enabled = true;
    }

    void OnGUI()
    {
        bool showMessage = Time.unscaledTime < hideAt;
        if (!recording && !showMessage) { enabled = false; return; }
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        if (recording)
        {
            // Rebuild the label once per second to keep the capture itself allocation-light.
            int second = (int)(Time.unscaledTime - recordingSince);
            if (second != shownSecond)
            {
                shownSecond = second;
                label = $"[REC] {second / 60:00}:{second % 60:00}  (F9: durdur)";
            }
            Draw(new Rect(12, 8, 480, 28), label, new Color(1f, .3f, .3f));
        }
        if (showMessage) Draw(new Rect(12, recording ? 34 : 8, 480, 28), message, Color.white);
    }

    void Draw(Rect rect, string text, Color color)
    {
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, style);
        style.normal.textColor = color;
        GUI.Label(rect, text, style);
    }
}
