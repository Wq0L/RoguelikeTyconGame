using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Batch (izole kopya): bir testi, projede belirli bir profil seçiliyken başlatır. Profil bağımsızlığı bununla denetlenir:
// aynı test farklı başlangıç seçimleriyle (ör. eski bir profil ve Run50_KirilmaV1) aynı sonucu vermelidir.
// Ortam değişkenleri: VERIFY_TARGET = test sınıfı (RunBatch'i çağrılır), VERIFY_START_PROFILE = profil asset adı ya da "none".
//   $env:VERIFY_TARGET = 'HarvestBehaviorVerification'; $env:VERIFY_START_PROFILE = 'Run50_DengeV1'
//   Tools/run-isolated-verification.ps1 -Method StartConditionRunner.Run -Full
// Yalnız izole kopyadaki seçim dosyası yazılır (her çalıştırmada gerçek projeden yeniden kopyalanır); testin kendisi değişmez.
public static class StartConditionRunner
{
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const string Profiles = "Assets/ScriptableObjects/RunProfiles/";

    public static void Run()
    {
        try
        {
            string target = Environment.GetEnvironmentVariable("VERIFY_TARGET");
            string start = Environment.GetEnvironmentVariable("VERIFY_START_PROFILE");
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(start)) throw new Exception("VERIFY_TARGET and VERIFY_START_PROFILE must be set");
            RunProfileSO profile = start == "none" ? null : AssetDatabase.LoadAssetAtPath<RunProfileSO>(Profiles + start + ".asset");
            if (start != "none" && profile == null) throw new Exception("profile not found: " + start);
            var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
            selection.active = profile;
            EditorUtility.SetDirty(selection);
            AssetDatabase.SaveAssets();
            Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(target)).FirstOrDefault(t => t != null);
            MethodInfo run = type != null ? type.GetMethod("RunBatch", BindingFlags.Public | BindingFlags.Static) : null;
            if (run == null) throw new Exception("no RunBatch on " + target);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/StartCondition.txt", $"{target} started with project selection {start}");
            run.Invoke(null, null);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/StartCondition.txt", "FAIL: " + ex);
            EditorApplication.Exit(2);
        }
    }
}
