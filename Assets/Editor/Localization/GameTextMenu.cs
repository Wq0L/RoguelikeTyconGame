using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Tools > Çeviri (4 Kasım 2026): tablo denetimi, Play'de dil değiştirme (sahte dil dahil) ve sabit yazıların listesi.
public static class GameTextMenu
{
    private const string Root = "Tools/Çeviri/";

    [MenuItem(Root + "Tabloyu denetle", priority = 0)]
    private static void Audit()
    {
        GameText.Reload();
        GameTextAudit.Report report = GameTextAudit.Run(GameText.SourceCsv);
        string missing = string.Join(", ", report.MissingPerLanguage.Select(p => $"{p.Key}: {p.Value} eksik"));
        foreach (string e in report.Errors) Debug.LogError("Çeviri tablosu: " + e);
        foreach (string w in report.Warnings) Debug.LogWarning("Çeviri tablosu: " + w);
        EditorUtility.DisplayDialog("Çeviri tablosu",
            $"{report.Keys} anahtar. {report.Errors.Count} hata, {report.Warnings.Count} uyarı.\n{missing}\n\nAyrıntılar Console'da.", "Tamam");
    }

    [MenuItem(Root + "Sabit yazıları listele (çeviri listesi taslağı)", priority = 1)]
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
