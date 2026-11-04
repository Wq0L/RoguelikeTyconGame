using UnityEditor;
using UnityEngine;

// Çeviri tablosu (Resources/Localization/GameText.csv) kaydedilince oyun yazıları yenilenir (4 Kasım 2026): Unity dosyayı yeniden
// içe aktarınca tablo yeniden kurulur ve GameText.Changed yayınlanır. Play açıkken bağlı yazılar (LocalizedText) ve açık
// konuşma kutusu hemen yeni metni gösterir. Excel / Google Sheets'te düzenleyip kaydetmek yeter.
public sealed class GameTextHotReload : AssetPostprocessor
{
    private const string TablePath = "Assets/Resources/" + GameText.ResourcePath + ".csv";

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (path != TablePath) continue;
            GameText.Reload();
            GameTextAudit.Report report = GameTextAudit.Run(GameText.SourceCsv);
            if (report.Ok) Debug.Log($"Çeviri tablosu yenilendi: {report.Keys} anahtar.");
            else Debug.LogWarning($"Çeviri tablosu yenilendi ama {report.Errors.Count} hata var (Tools > Çeviri > Tabloyu denetle): {report.Errors[0]}");
            return;
        }
    }
}
