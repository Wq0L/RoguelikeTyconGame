using UnityEditor;
using UnityEngine;

// Hikâye Play testi (Faz 3, 5 Kasım 2026): R25'e ya da zafere oynayarak gitmeden taşları sırayla denemek için.
// Yalnız Play'de. Gerçek kayda yazar (taşlar, aşama, cevap); baştan almak için "Görülen konuşmaları sıfırla" (Play dışında).
public static class StoryTestMenu
{
    const string Next = "Tools/Diyalog/Hikâye: sıradaki taşı şimdi kazan (Play)";
    const string Status = "Tools/Diyalog/Hikâye: durumu yaz (Play)";

    // Kazanılmamış ilk taş kazanılır; sakin bir ekrandaysan (hazırlık, round sonu) konuşması hemen açılır, değilse sıradaki sakin anda.
    [MenuItem(Next)]
    private static void WinNext()
    {
        string id = null;
        foreach (string s in DialogueProgress.StoryOrder)
            if (!DialogueProgress.HasMilestone(s)) { id = s; break; }
        if (id == null) { Debug.Log("Hikâye: bütün taşlar zaten kazanılmış."); return; }
        DialogueProgress.AddMilestone(id);
        DialogueDirector director = DialogueDirector.Instance;
        if (director != null) { director.ResetStoryMoment(); director.ForceStoryCheck(); }
        Debug.Log($"Hikâye: '{id}' taşı kazanıldı. Sıradaki konuşma: {DialogueProgress.NextStory() ?? "yok"} · aşama {DialogueProgress.Stage}" +
                  (director != null && director.Current != null ? $" · açık: {director.Current.id}" : " · sakin bir ekranda (hazırlık / round sonu) açılır"));
    }

    [MenuItem(Status)]
    private static void WriteStatus()
    {
        var reached = new System.Collections.Generic.List<string>();
        foreach (string s in DialogueProgress.StoryOrder)
            reached.Add(s + (DialogueProgress.Seen(s) ? " ✓" : DialogueProgress.HasMilestone(s) ? " (kazanıldı, bekliyor)" : ""));
        Debug.Log($"Hikâye: aşama {DialogueProgress.Stage} · Taş 1 cevabı: {DialogueProgress.Choice(DialogueProgress.TrialPassed) ?? "yok"} · " +
                  $"sıradaki: {DialogueProgress.NextStory() ?? "yok"}\n" + string.Join("\n", reached));
    }

    [MenuItem(Next, true)] private static bool CanWinNext() => EditorApplication.isPlaying;
    [MenuItem(Status, true)] private static bool CanStatus() => EditorApplication.isPlaying;
}
