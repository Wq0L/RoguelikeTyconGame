using System.IO;
using UnityEditor;
using UnityEngine;

// Play testi için (28 Ekim 2026): gerçek kayıttaki yalnız diyalog / hikâye ilerlemesini sıfırlar; öğretim satırları baştan çıkar.
// Çiftçi / tırpan seçimi, açılan kartlar ve görevler korunur. Play dışında çalışır (oyun açıkken kayıt bellekte tutulur, üzerine yazılırdı).
// Önce meta.json'un bir kopyası alınır (meta.json.before-dialogue-reset). Faz 4'te ayarlar menüsüne "Rehberi sıfırla" gelecek.
public static class DialogueResetMenu
{
    [MenuItem("Tools/Diyalog/Görülen konuşmaları sıfırla (Play testi için)")]
    private static void Reset()
    {
        MetaSave.Load();
        if (!File.Exists(MetaSave.FilePath))
        {
            EditorUtility.DisplayDialog("Diyalog", "Kayıt dosyası yok: zaten temiz, bütün konuşmalar baştan çıkacak.", "Tamam");
            return;
        }
        MetaSaveData data = MetaSave.Data;
        int seen = data.seenDialogue.Count;
        if (!EditorUtility.DisplayDialog("Diyalog",
                $"Görülen {seen} konuşma, bekleyen hikâye, kilometre taşları ve hikâye aşaması ({data.storyStage}) sıfırlanacak.\n" +
                "Çiftçi / tırpan, açılan kartlar ve görevler korunur. Önce yedek alınır.", "Sıfırla", "Vazgeç")) return;
        File.Copy(MetaSave.FilePath, MetaSave.FilePath + ".before-dialogue-reset", true);
        data.seenDialogue.Clear();
        data.pendingStory.Clear();
        data.milestones.Clear();
        data.storyStage = 0;
        data.introSeen = false;
        bool saved = MetaSave.Save();
        Debug.Log(saved ? $"Diyalog ilerlemesi sıfırlandı ({seen} konuşma). Yedek: {MetaSave.FilePath}.before-dialogue-reset"
                        : "Diyalog ilerlemesi sıfırlanamadı (kayıt yazılamadı; daha yeni sürüm kaydı olabilir).");
        MetaSave.Load();
    }

    [MenuItem("Tools/Diyalog/Görülen konuşmaları sıfırla (Play testi için)", true)]
    private static bool CanReset() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
