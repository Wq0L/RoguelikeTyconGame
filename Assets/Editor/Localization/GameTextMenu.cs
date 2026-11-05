using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Tools > Çeviri (4–5 Kasım 2026): kurulum ve listeyi tabloya aktarma, liste denetimi, Play'de dil değiştirme (sahte dil dahil),
// sabit yazıların listesi.
public static class GameTextMenu
{
    private const string Root = "Tools/Çeviri/";

    [MenuItem(Root + "Kurulum ve listeyi tabloya aktar", priority = 0)]
    private static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Çeviri", "Play dışında çalıştırın.", "Tamam"); return; }
        LocalizationSetup.ImportReport r;
        try { r = LocalizationSetup.Run(); }
        catch (LocalizationSetup.InvalidListException e) { EditorUtility.DisplayDialog("Çeviri listesi hatalı", e.Message, "Tamam"); return; }
        GameText.Reload();
        EditorUtility.DisplayDialog("Çeviri",
            $"Unity Localization hazır. Diller: {string.Join(", ", r.Languages)}.\n" +
            $"GameText tablosu: {r.Added} yeni, {r.Updated} değişen, {r.Removed} silinen, {r.Unchanged} aynı anahtar ({r.Smart} Smart hücre).\n\n" +
            $"Ana liste: {GameText.MasterCsvPath}", "Tamam");
    }

    [MenuItem(Root + "Listeyi denetle", priority = 1)]
    private static void Audit()
    {
        GameTextAudit.Report report = GameTextAudit.Run(GameText.SourceCsv);
        string missing = string.Join(", ", report.MissingPerLanguage.Select(p => $"{p.Key}: {p.Value} eksik"));
        foreach (string e in report.Errors) Debug.LogError("Çeviri listesi: " + e);
        foreach (string w in report.Warnings) Debug.LogWarning("Çeviri listesi: " + w);
        EditorUtility.DisplayDialog("Çeviri listesi",
            $"{report.Keys} anahtar. {report.Errors.Count} hata, {report.Warnings.Count} uyarı.\n{missing}\n\nAyrıntılar Console'da.", "Tamam");
    }

    [MenuItem(Root + "Sabit yazıları listele (çeviri listesi taslağı)", priority = 2)]
    private static void ListHardcoded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Çeviri", "Play dışında çalıştırın (sahneler açılıp taranır).", "Tamam"); return; }
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "Ceviri", "SabitYazilar.csv"));
        var items = HardcodedTextScanner.Scan();
        HardcodedTextScanner.WriteCsv(items, path);
        EditorUtility.DisplayDialog("Çeviri", $"{items.Count} sabit yazı bulundu.\n{HardcodedTextScanner.Summary(items)}\n\n{path}", "Tamam");
        EditorUtility.RevealInFinder(path);
    }

    [MenuItem(Root + "Dil (Play): Türkçe", priority = 20)] private static void Tr() => GameText.Language = "tr";
    [MenuItem(Root + "Dil (Play): English", priority = 21)] private static void En() => GameText.Language = "en";
    [MenuItem(Root + "Dil (Play): Sahte dil (sabit yazıları bulmak için)", priority = 22)] private static void Pseudo() => GameText.Language = GameText.PseudoLanguage;

    [MenuItem(Root + "Dil (Play): Türkçe", true)]
    [MenuItem(Root + "Dil (Play): English", true)]
    [MenuItem(Root + "Dil (Play): Sahte dil (sabit yazıları bulmak için)", true)]
    private static bool InPlay() => EditorApplication.isPlaying;
}
