using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Sahne ve prefab yazılarını bağlama doğrulaması (5 Kasım 2026). Yalnız izole kopyada:
//   1. gözden geçirilmiş plan (CLICKER_BIND_PLAN) TextBindingTool.Apply ile uygulanır
//   2. her "bagla" satırının nesnesinde LocalizedText doğru anahtarla var; anahtarlar ana listede ve Unity tablosunda, iki dilde
//   3. ikinci uygulama hiçbir şey değiştirmez
//   4. Play: MenuScene'de "Play" düğmesi Türkçe "Oyna", İngilizceye geçince "Play"
// Çalıştırma: $env:CLICKER_BIND_PLAN='<plan.csv>'; Tools/run-isolated-verification.ps1 -Method BindingVerification.RunBatch -Full
[InitializeOnLoad]
public static class BindingVerification
{
    const string Key = "BindingVerification", EditNotes = "Logs/BindingVerification_edit.txt";
    static readonly List<string> notes = new();
    static double next;
    static int step;

    static BindingVerification() { EditorApplication.update += Tick; }
    static void Check(bool value, string text) { if (!value) throw new Exception(text); notes.Add("PASS " + text); }

    public static void RunBatch()
    {
        try
        {
            if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
            EditorUtility.audioMasterMute = true;
            string plan = Environment.GetEnvironmentVariable("CLICKER_BIND_PLAN");
            if (string.IsNullOrEmpty(plan) || !File.Exists(plan)) throw new Exception("CLICKER_BIND_PLAN missing");
            LocalizationSetup.Run();
            List<TextBindingTool.Row> rows = TextBindingTool.ReadPlan(plan);
            List<TextBindingTool.Row> bind = rows.Where(r => r.Action == TextBindingTool.Bind).ToList();
            int keys = bind.Select(r => r.Key).Distinct().Count();

            TextBindingTool.ApplyReport first = TextBindingTool.Apply(rows);
            notes.Add("INFO first apply: " + first + (first.Skipped.Count > 0 ? " | " + string.Join(" | ", first.Skipped) : ""));
            Check(first.Skipped.Count == 0 && first.Bound == bind.Count && first.KeysAdded == keys, $"plan applied: {keys} keys added, {bind.Count} labels bound, nothing skipped");

            string csv = File.ReadAllText(GameText.MasterCsvPath);
            Check(GameTextAudit.Run(csv).Ok, "master list still passes the audit");
            Check(LocalizationBuildCheck.Problems().Count == 0, "tables match the list after the apply (pre-build check clean)");

            foreach (IGrouping<string, TextBindingTool.Row> group in bind.GroupBy(r => r.Location))
            {
                IList<GameObject> roots; Scene scene = default; GameObject prefabRoot = null;
                if (group.Key.EndsWith(".unity")) { scene = EditorSceneManager.OpenScene(group.Key, OpenSceneMode.Single); roots = scene.GetRootGameObjects(); }
                else { prefabRoot = PrefabUtility.LoadPrefabContents(group.Key); roots = new[] { prefabRoot }; }
                foreach (TextBindingTool.Row r in group)
                {
                    Transform t = TextBindingTool.Resolve(roots, r.ObjectPath);
                    var loc = t != null ? t.GetComponent<LocalizedText>() : null;
                    if (loc == null || loc.Key != r.Key) throw new Exception($"not bound: {group.Key} {r.ObjectPath}");
                }
                if (prefabRoot != null) PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
            notes.Add("PASS every planned label carries LocalizedText with its key (scenes and prefab saved)");

            foreach (var g in bind.GroupBy(r => r.Key))
            {
                TextBindingTool.Row r = g.First();
                GameText.Language = "en";
                string en = GameText.Get(r.Key);
                GameText.Language = "tr";
                string tr = GameText.Get(r.Key);
                if (en != r.En || tr != r.Tr) throw new Exception($"{r.Key}: en '{en}' tr '{tr}' (plan '{r.En}' / '{r.Tr}')");
            }
            notes.Add("PASS every new key reads back in English and Turkish from the Unity tables");

            TextBindingTool.ApplyReport again = TextBindingTool.Apply(rows);
            Check(again.Bound == 0 && again.KeysAdded == 0 && again.AlreadyBound == bind.Count && again.Skipped.Count == 0, "applying the same plan again changes nothing: " + again);

            Directory.CreateDirectory("Logs");
            File.WriteAllLines(EditNotes, notes);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/MenuScene.unity", true), new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true) };
            EditorSceneManager.OpenScene("Assets/Scenes/MenuScene.unity", OpenSceneMode.Single);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Finish(e); }
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            switch (step++)
            {
                case 0: next = EditorApplication.timeSinceStartup + 2; return;
                case 1:
                {
                    LocalizedText play = UnityEngine.Object.FindObjectsByType<LocalizedText>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(l => l.Key == "ui.menu.play");
                    Check(play != null, "MenuScene in play has the bound Play label");
                    TMP_Text label = play.GetComponent<TMP_Text>();
                    Check(GameText.Language == "tr" && label.text == "Oyna<br>", $"Turkish menu shows 'Oyna' ({label.text})");
                    GameText.Language = "en";
                    Check(label.text == "Play<br>", $"switching to English updates the button at once ({label.text})");
                    GameText.Language = "tr";
                    Check(label.text == "Oyna<br>", "and back to Turkish");
                    Finish(null);
                    return;
                }
            }
        }
        catch (Exception e) { Finish(e); }
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        if (ex != null) notes.Add("FAIL " + ex.Message);
        Directory.CreateDirectory("Logs");
        if (EditorApplication.isPlaying && File.Exists(EditNotes)) notes.InsertRange(0, File.ReadAllLines(EditNotes));
        notes.Add($"SUMMARY {notes.Count(n => n.StartsWith("PASS"))} PASS, {notes.Count(n => n.StartsWith("FAIL"))} FAIL");
        File.WriteAllLines("Logs/BindingVerification.txt", notes);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        EditorApplication.Exit(ex == null ? 0 : 1);
    }
}
