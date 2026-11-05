using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

// Gerçek oyun derlemesinde dil testi (5 Kasım 2026). Yalnız izole kopyada:
//   1. kurulum ve aktarım (LocalizationSetup.Run), derleme öncesi denetim (LocalizationBuildCheck) temiz olmalı
//   2. Windows oyuncusu derlenir (yalnız MenuScene; Addressables tabloları derlemeyle birlikte paketlenir)
//   3. oyun -batchmode -nographics -localization-smoke-test ile açılır; LocalizationSmokeTest her dilde yazıları okur, günlüğe yazar
//   4. günlükteki [LOC-SMOKE] satırı ve çıkış kodu kontrol edilir
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method LocalizationBuildSmoke.RunBatch -Full -TimeoutSec 2400
// Çıktı: Logs/LocalizationBuildSmoke.txt, oyun günlüğü Logs/LocSmoke_player.log. Derleme: Builds/LocSmoke (kopyanın içinde).
public static class LocalizationBuildSmoke
{
    public static void RunBatch()
    {
        var notes = new List<string>();
        int exit = 1;
        try
        {
            if (!UnityEngine.Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
            // Etkin platform derlenen platform olmalı (değilse Addressables paketleri yanlış klasöre yazılır; ilk çalıştırmada böyle oldu).
            BuildTarget was = EditorUserBuildSettings.activeBuildTarget;
            if (was != BuildTarget.StandaloneWindows64)
            {
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                    throw new Exception("could not switch the active platform to Windows");
                notes.Add($"INFO active platform switched {was} → StandaloneWindows64");
            }
            bool caught = false;
            try { LocalizationBuildCheck.Check(BuildTarget.WebGL); }
            catch (UnityEditor.Build.BuildFailedException e) { caught = e.Message.Contains("etkin platform değil"); }
            if (!caught) throw new Exception("pre-build check did not stop a build for a platform other than the active one");
            notes.Add("PASS pre-build check stops a build whose platform differs from the active one (wrong bundle folder)");
            LocalizationSetup.Run();
            List<string> problems = LocalizationBuildCheck.Problems();
            if (problems.Count > 0) throw new Exception("pre-build check: " + string.Join("; ", problems));
            notes.Add("PASS pre-build check is clean (tables match the list, Addressables entries, preload, build-with-player)");

            string exe = Path.GetFullPath("Builds/LocSmoke/LocSmoke.exe");
            if (Directory.Exists(Path.GetDirectoryName(exe))) Directory.Delete(Path.GetDirectoryName(exe), true);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MenuScene.unity" },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("player build " + report.summary.result + " (" + report.summary.totalErrors + " errors)");
            notes.Add($"PASS player built ({report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime.TotalSeconds:0} s)");
            string data = Path.Combine(Path.GetDirectoryName(exe), "LocSmoke_Data", "StreamingAssets", "aa");
            if (!Directory.Exists(data) || !Directory.GetFiles(data, "*.bundle", SearchOption.AllDirectories).Any())
                throw new Exception("no Addressables bundles in the build (" + data + ")");
            notes.Add("PASS Addressables bundles are inside the build");

            string log = Path.GetFullPath("Logs/LocSmoke_player.log");
            if (File.Exists(log)) File.Delete(log);
            var start = new ProcessStartInfo(exe, $"-batchmode -nographics {LocalizationSmokeTest.Flag} -logFile \"{log}\"")
            { UseShellExecute = false, CreateNoWindow = true };
            using (Process player = Process.Start(start))
            {
                bool quit = player.WaitForExit(120000);
                if (!quit) player.Kill();
                // Oyunun bütün test satırları (aşama aşama): takılırsa nerede takıldığı görünür.
                List<string> marks = File.Exists(log)
                    ? File.ReadAllLines(log).Where(l => l.Contains(LocalizationSmokeTest.Marker)).ToList() : new List<string>();
                foreach (string m in marks) notes.Add("INFO " + m.Trim());
                if (!quit) throw new Exception("player did not quit in 120 s (no watchdog either: blocked before the test hooks?)");
                string result = marks.LastOrDefault(l => l.Contains(" PASS |") || l.Contains(" FAIL |") || l.Contains("TIMEOUT"));
                if (result == null) throw new Exception($"no result line in the player log (exit {player.ExitCode})");
                if (player.ExitCode != 0 || !result.Contains(" PASS |")) throw new Exception($"smoke test failed in the real build (exit {player.ExitCode})");
                notes.Add("PASS real build: texts come from the Unity tables in every language (no keys shown)");
            }
            exit = 0;
        }
        catch (Exception e) { notes.Add("FAIL " + e.Message); }
        Directory.CreateDirectory("Logs");
        notes.Add($"SUMMARY {notes.Count(n => n.StartsWith("PASS"))} PASS, {notes.Count(n => n.StartsWith("FAIL"))} FAIL");
        File.WriteAllLines("Logs/LocalizationBuildSmoke.txt", notes);
        EditorApplication.Exit(exit);
    }
}
