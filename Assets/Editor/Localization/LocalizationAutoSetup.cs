using System;
using UnityEditor;
using UnityEngine;

// Unity Localization kurulumu eksik YA DA bugünkü kurallardan farklıysa (LocalizationSetup.ConfigProblems: ör. eski sürümün
// en ↔ tr dil döngüsü) editör açılınca / kod derlenince bir kez kendiliğinden yapılır / güncellenir (5 Kasım 2026): ayar dosyası,
// diller, dil yedekleri ve GameText tablosu, ana liste (Assets/Localization/GameText.csv) aktarılır. Güncelse bir şey yapmaz.
// Play sırasında ya da derleme / içe aktarma sürerken beklemeye alınır. Elle: Tools > Çeviri > Kurulum.
[InitializeOnLoad]
public static class LocalizationAutoSetup
{
    static LocalizationAutoSetup() => EditorApplication.delayCall += TryRun;

    // Test: kurulum çalışmalı mı.
    public static bool ShouldRun => System.IO.File.Exists(GameText.MasterCsvPath) && !LocalizationSetup.IsUpToDate;

    private static void TryRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryRun;
            return;
        }
        if (!ShouldRun) return;
        bool fresh = !LocalizationSetup.IsComplete;
        string reasons = fresh ? "kurulum yoktu" : string.Join("; ", LocalizationSetup.ConfigProblems());
        try
        {
            LocalizationSetup.ImportReport r = LocalizationSetup.Run();
            GameText.Reload();
            Debug.Log($"Unity Localization {(fresh ? "kuruldu" : "güncellendi")} ({reasons}): diller {string.Join(", ", r.Languages)}, " +
                      $"GameText tablosu {r.Added} yeni, {r.Updated} değişen, {r.Removed} silinen. Yazılar Assets/Localization/GameText.csv'den düzenlenir.");
        }
        catch (Exception e)
        {
            Debug.LogError("Unity Localization kurulumu yapılamadı (Tools > Çeviri > Kurulum ile tekrar deneyin): " + e);
        }
    }
}
