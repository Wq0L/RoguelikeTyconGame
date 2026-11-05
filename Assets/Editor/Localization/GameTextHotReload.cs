using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

// Ana liste (Assets/Localization/GameText.csv) kaydedilince (4–5 Kasım 2026): Unity dosyayı yeniden içe aktarır; liste ÖNCE
// denetlenir. Hatalıysa (kapanmamış tırnak, yinelenen anahtar, boş İngilizce, yer tutucu farkı …) tablolara dokunulmaz, oyun eski
// metni göstermeye devam eder ve Console'a hata yazılır. Geçerliyse Unity Localization'ın "GameText" tablosuna aktarılır
// (LocalizationSetup.Import), GameText.Changed yayınlanır: Play açıkken bağlı yazılar ve açık konuşma kutusu hemen yenilenir.
// Kurulum yapılmamışsa yalnız CSV yeniden okunur (editörde; GameText bunu ayrıca uyarır). Tablo dosyaları sonra diske yazılır.
public sealed class GameTextHotReload : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (path != GameText.MasterCsvPath) continue;
            string csv = System.IO.File.ReadAllText(path);
            if (!LocalizationSetup.IsComplete)
            {
                GameTextAudit.Report audit = GameTextAudit.Run(csv);
                if (!audit.Ok) Debug.LogError($"Çeviri listesi hatalı ({audit.Errors.Count} hata):\n{audit.Summary()}");
                GameText.Reload();
                return;
            }
            try
            {
                LocalizationSetup.ImportReport r = LocalizationSetup.Import(csv, LocalizationEditorSettings.GetStringTableCollection(GameText.TableName));
                EditorApplication.delayCall += AssetDatabase.SaveAssets;
                GameText.Reload();
                Debug.Log($"Çeviri listesi aktarıldı: tablo {r.Added} yeni, {r.Updated} değişen, {r.Removed} silinen, {r.Unchanged} aynı.");
            }
            catch (LocalizationSetup.InvalidListException e)
            {
                Debug.LogError(e.Message + "\nDüzeltip kaydedin; Tools > Çeviri > Listeyi denetle ayrıntıyı gösterir.");
            }
            return;
        }
    }
}
