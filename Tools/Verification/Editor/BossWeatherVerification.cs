using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BossWeatherVerification
{
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    public static void RunBatch()
    {
        Directory.CreateDirectory("Logs");
        try
        {
            var definitions = new SegmentEventSO[] { ScriptableObject.CreateInstance<FogSO>(), ScriptableObject.CreateInstance<FrostFrontSO>(), ScriptableObject.CreateInstance<HardShellSO>() };
            for (int i = 0; i < definitions.Length; i++)
            {
                var runtime = definitions[i].CreateRuntime(SegmentEventTiming.BossRound(1, 5, 123));
                Check(BossWeatherOverlay.Resolve(runtime, true) == BossWeatherOverlay.Weather.None, "Preview must have no weather");
                runtime.Activate();
                Check((int)BossWeatherOverlay.Resolve(runtime, true) == i + 1, "Active event weather mapping");
                Check(BossWeatherOverlay.Resolve(runtime, false) == BossWeatherOverlay.Weather.None, "Round end must hide weather even for legacy multi-round event");
                runtime.Finish();
                Check(BossWeatherOverlay.Resolve(runtime, true) == BossWeatherOverlay.Weather.None, "Finished boss must hide weather");
                UnityEngine.Object.DestroyImmediate(definitions[i]);
            }
            Check(BossWeatherOverlay.Resolve(null, true) == BossWeatherOverlay.Weather.None, "Cleared director hides weather");
            Shader shader = Resources.Load<Shader>("BossWeather");
            Check(shader != null, "Shader included in Resources");
            Check(!ShaderUtil.ShaderHasError(shader), "Shader imports without errors");
            var previewMaterial = new Material(shader);
            Check(previewMaterial.HasProperty("_MainTex"), "RawImage requires _MainTex even for procedural weather");
            var target = new RenderTexture(960, 540, 0);
            target.Create();
            var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            for (int weather = 1; weather <= 3; weather++)
            {
                RenderTexture.active = target;
                GL.Clear(true, true, new Color(.12f, .19f, .23f));
                previewMaterial.SetFloat("_Weather", weather);
                previewMaterial.SetFloat("_WeatherTime", 8);
                previewMaterial.SetFloat("_Strength", .65f);
                previewMaterial.SetFloat("_Aspect", 960f / 540);
                Graphics.Blit(Texture2D.whiteTexture, target, previewMaterial);
                pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                pixels.Apply();
                File.WriteAllBytes($"Logs/BossWeather{weather}.png", pixels.EncodeToPNG());
            }
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(previewMaterial);
            var overlay = BossWeatherOverlay.Ensure();
            Check(BossWeatherOverlay.Ensure() == overlay, "Ensure does not duplicate overlay");
            // Awake is not automatically called by AddComponent outside Play mode.
            if (overlay.GetComponent<Canvas>() == null) overlay.SendMessage("Awake");
            var image = overlay.GetComponentInChildren<RawImage>();
            Check(image != null && !image.raycastTarget, "Weather does not intercept input");
            Check(overlay.GetComponent<Canvas>().sortingOrder < 0, "Weather renders below normal HUD");
            string uiError = null;
            Application.LogCallback capture = (message, trace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception) uiError = message;
            };
            Application.logMessageReceived += capture;
            image.enabled = true;
            image.material.SetFloat("_Weather", 2);
            image.material.SetFloat("_Strength", .65f);
            Canvas.ForceUpdateCanvases();
            Application.logMessageReceived -= capture;
            Check(uiError == null, "Active RawImage canvas rebuild has no material error: " + uiError);
            overlay.SendMessage("LateUpdate");
            Check(!image.enabled, "No active run leaves overlay hidden");
            overlay.enabled = false;
            Check(overlay.CurrentWeather == BossWeatherOverlay.Weather.None, "Disable clears state");
            UnityEngine.Object.DestroyImmediate(overlay.gameObject);
            File.WriteAllText("Logs/BossWeatherVerification.txt", $"PASS: {checks} checks\nPreview, active event types, round end, cleared director, shader, duplicate prevention, input and HUD layering.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { File.WriteAllText("Logs/BossWeatherVerification.txt", "FAIL: " + e); EditorApplication.Exit(1); }
    }
}
