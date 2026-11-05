using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

// Oyun derlenmeden önce çeviri denetimi (5 Kasım 2026). Editörde yazılar tablo eksikken de CSV'den görünür; paketlenmiş oyunda
// CSV yoktur. Bu yüzden "editörde görünüyor" paketleme doğrulaması sayılmaz: derleme şunlar tutmazsa açık bir mesajla durur.
//   - Unity Localization kurulumu tamam (ayar, kaynak dil, GameText tablosu)
//   - ana liste (GameText.csv) denetimden geçiyor
//   - tablolar listeyle birebir güncel (her anahtar, her dilin hücresi; listede olmayan anahtar yok)
//   - her dil tablosu ve ortak anahtar verisi Addressables'a kayıtlı, tablolar önceden yükleniyor
//   - Addressables oyuncu derlemesiyle birlikte paketleniyor
//   - derlenen platform = etkin platform. Değilse Addressables paketleri eski platformun klasörüne yazar, oyuna yalnız katalog
//     gider, tablolar gitmez (5 Kasım: gerçek derleme testinde yakalandı; etkin platform WebGL iken Windows derlendi).
// Bu denetim Addressables paket üretmeden önce çalışır (BuildPlayerProcessor, sıra 0 < Addressables 1); ikinci kez de derleme
// başlarken (IPreprocessBuildWithReport). Elle: Tools > Çeviri > Derleme öncesi denetim. Gerçek derlemede dil testi:
// LocalizationSmokeTest.
// Addressables paketlerinden önce (sıra 0; Addressables'ın derleme hazırlığı 1).
public sealed class LocalizationBuildCheckEarly : BuildPlayerProcessor
{
    public override int callbackOrder => 0;
    public override void PrepareForBuild(BuildPlayerContext context) => LocalizationBuildCheck.Check(context.BuildPlayerOptions.target);
}

public sealed class LocalizationBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => -100;

    public void OnPreprocessBuild(BuildReport report) => Check(report.summary.platform);

    public static void Check(BuildTarget target)
    {
        List<string> problems = Problems();
        if (target != EditorUserBuildSettings.activeBuildTarget)
            problems.Insert(0, $"derlenen platform ({target}) etkin platform değil ({EditorUserBuildSettings.activeBuildTarget}); çeviri paketleri yanlış " +
                               "klasöre yazılır. Önce File > Build Profiles'tan platformu değiştirin");
        if (problems.Count > 0)
            throw new BuildFailedException("Çeviri denetimi derlemeyi durdurdu:\n- " + string.Join("\n- ", problems) +
                                           "\nDüzeltme çoğunlukla: listeyi düzeltip kaydet ya da Tools > Çeviri > Kurulum.");
    }

    [MenuItem("Tools/Çeviri/Derleme öncesi denetim", priority = 3)]
    private static void Menu()
    {
        List<string> problems = Problems();
        EditorUtility.DisplayDialog("Çeviri", problems.Count == 0 ? "Derlemeye hazır: tablolar listeyle güncel, paketleme ayarları doğru."
            : "Sorunlar:\n- " + string.Join("\n- ", problems.Take(12)), "Tamam");
    }

    public static List<string> Problems()
    {
        var problems = new List<string>();
        if (!LocalizationSetup.IsComplete) { problems.Add("Unity Localization kurulumu eksik"); return problems; }
        foreach (string p in LocalizationSetup.ConfigProblems()) problems.Add("ayar güncel değil: " + p);
        if (!File.Exists(GameText.MasterCsvPath)) { problems.Add("ana liste yok: " + GameText.MasterCsvPath); return problems; }
        string csv = File.ReadAllText(GameText.MasterCsvPath);
        GameTextAudit.Report audit = GameTextAudit.Run(csv);
        if (!audit.Ok) { problems.Add($"liste hatalı ({audit.Errors.Count}): {audit.Errors[0]}"); return problems; }

        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(GameText.TableName);
        List<List<string>> rows = GameText.Parse(csv);
        List<string> header = rows[0].ConvertAll(h => h.Trim().ToLowerInvariant());
        var keys = new HashSet<string>();
        for (int c = 1; c < header.Count; c++)
        {
            if (!GameText.IsLanguageColumn(header[c])) continue;
            if (collection.GetTable(new LocaleIdentifier(header[c])) is not StringTable table) { problems.Add("dil tablosu yok: " + header[c]); continue; }
            int stale = 0;
            for (int r = 1; r < rows.Count; r++)
            {
                List<string> row = rows[r];
                if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0])) continue;
                string key = row[0].Trim();
                keys.Add(key);
                string cell = c < row.Count && !GameText.IsBlank(row[c]) ? row[c] : "";
                string value = table.GetEntry(key)?.Value ?? "";
                if (value != cell) stale++;
            }
            if (stale > 0) problems.Add($"{header[c]} tablosu listeyle güncel değil ({stale} hücre)");
        }
        int extra = collection.SharedData.Entries.Count(e => !keys.Contains(e.Key));
        int missing = keys.Count(k => !collection.SharedData.Contains(k));
        if (extra > 0 || missing > 0) problems.Add($"tablo anahtarları listeyle aynı değil ({missing} eksik, {extra} fazla)");

        AddressableAssetSettings aa = AddressableAssetSettingsDefaultObject.Settings;
        if (aa == null) { problems.Add("Addressables ayarı yok"); return problems; }
        if (aa.BuildAddressablesWithPlayerBuild != AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer)
            problems.Add("Addressables oyuncu derlemesiyle paketlenmiyor (BuildAddressablesWithPlayerBuild)");
        foreach (StringTable table in collection.StringTables)
        {
            if (table == null) continue;
            if (aa.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(table))) == null)
                problems.Add("tablo Addressables'a kayıtlı değil: " + table.name);
            if (!LocalizationEditorSettings.GetPreloadTableFlag(table)) problems.Add("tablo önceden yüklenmiyor: " + table.name);
        }
        if (aa.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(collection.SharedData))) == null)
            problems.Add("ortak anahtar verisi Addressables'a kayıtlı değil");
        return problems;
    }
}
