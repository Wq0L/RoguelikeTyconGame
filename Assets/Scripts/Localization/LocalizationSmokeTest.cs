using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization.Settings;

// Gerçek oyun derlemesinde kısa dil testi (5 Kasım 2026). Editörde yazılar tablo eksikken de CSV'den görünür; paketlenmiş oyunda
// görünüp görünmediğini yalnız gerçek derleme gösterir. Oyun "-localization-smoke-test" ile açılırsa:
//   - ses en başta kapatılır;
//   - ilk sahne yüklenince Unity Localization'ın açılış yüklemesi karelerle (beklemeden) izlenir, her aşama günlüğe yazılır;
//   - yükleme bitince her dilde birkaç anahtar okunur (Unity tablolarından mı geliyor, anahtar mı görünüyor);
//   - sonuç oyuncu günlüğüne "[LOC-SMOKE]" satırıyla yazılır ve oyun kapanır (geçti 0, kaldı 3, takıldı 4).
//   - bekçi: 60 sn içinde bitmezse başka bir iş parçacığından "TIMEOUT" yazılıp süreç kapatılır (takılan beklemeyi de yakalar).
// Bayrak yoksa hiçbir şey yapmaz. Derleyip çalıştıran test: Tools/Verification/Editor/LocalizationBuildSmoke.cs.
public static class LocalizationSmokeTest
{
    public const string Flag = "-localization-smoke-test", Marker = "[LOC-SMOKE]";
    private static readonly string[] Keys = { "ui.skip", "ui.name.eri", "tut.hit.0" };
    private static Timer watchdog;

    private static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), Flag) >= 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (!Requested) return;
        AudioListener.volume = 0f;
        AudioListener.pause = true;
        Debug.Log($"{Marker} boot");
        watchdog = new Timer(_ =>
        {
            Console.WriteLine($"{Marker} TIMEOUT (60 s)");
            Debug.Log($"{Marker} TIMEOUT (60 s)");
            Environment.Exit(4);
        }, null, 60000, Timeout.Infinite);
        // En erken an: ilk sahne yüklenmeden önce (sahnedeki yazılar Awake / OnEnable'da bundan hemen sonra okur). Beklemeli yükleme
        // burada takılırsa bekçi yakalar ve günlükteki son satır bu aşamayı gösterir.
        Debug.Log($"{Marker} read before scene load…");
        Debug.Log($"{Marker} before scene load: '{GameText.Get("ui.skip")}' (unity tables {GameText.UsingUnityTables})");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Run()
    {
        if (!Requested) return;
        Debug.Log($"{Marker} scene loaded: '{GameText.Get("tut.hit.0")}'");
        var host = new GameObject("Localization Smoke Test");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<Runner>();
    }

    private sealed class Runner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var init = LocalizationSettings.InitializationOperation;
            Debug.Log($"{Marker} init started (done {init.IsDone})");
            float t = 0f;
            while (!init.IsDone && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
            Debug.Log($"{Marker} init done {init.IsDone} status {init.Status} after {t:0.00} s {(init.OperationException != null ? init.OperationException.Message : "")}");

            var lines = new List<string>();
            bool ok = init.IsDone && GameText.UsingUnityTables;
            lines.Add("unity tables " + (ok ? "yes" : "NO"));
            foreach (string lang in new List<string>(GameText.Languages))
            {
                GameText.Language = lang;
                yield return null;
                foreach (string key in Keys)
                {
                    string text = GameText.Get(key);
                    bool good = !string.IsNullOrEmpty(text) && text != key;
                    ok &= good;
                    lines.Add($"{lang}:{key}='{(text.Length > 24 ? text.Substring(0, 24) + "…" : text)}'{(good ? "" : " FAIL")}");
                }
            }
            ok &= GameText.Languages.Count >= 2;

            // Ayarlar menüsündeki gibi: dil değişir ve yazılar AYNI karede okunur (bağlı yazılar Changed ile hemen yenilenir).
            foreach (string lang in new List<string>(GameText.Languages))
            {
                Debug.Log($"{Marker} same-frame switch to {lang}…");
                GameText.Language = lang;
                string text = GameText.Get("ui.skip");
                bool good = !string.IsNullOrEmpty(text) && text != "ui.skip";
                ok &= good;
                lines.Add($"same-frame {lang}:'{text}'{(good ? "" : " FAIL")}");
                Debug.Log($"{Marker} same-frame {lang} read '{text}'");
            }
            Debug.Log($"{Marker} {(ok ? "PASS" : "FAIL")} | {string.Join(" | ", lines)}");
            watchdog?.Dispose();
            Application.Quit(ok ? 0 : 3);
        }
    }
}
