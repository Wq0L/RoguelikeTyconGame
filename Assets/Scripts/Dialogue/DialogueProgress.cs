using System;
using System.Collections.Generic;

// Diyalog ve hikâye ilerlemesinin kalıcı kaydı (MetaSave.Data üzerinde; ayrı dosya yok).
// - Görülen konuşmalar: oyun içinde kümeye çevrilir (hızlı arama); kayıt nesnesi değişirse (yeniden yükleme) küme yenilenir.
// - Satır görülünce diske hemen yazılmaz (MarkDirty); hikâye aşaması, cevap seçimi ve giriş videosu gibi önemli anlar hemen yazılır.
// Hikâye (Faz 3, 5 Kasım 2026 kesin plan: Docs/DiyalogPlani/Faz3-KilometreTaslari-Taslak.md "KESİN PLAN v3"):
// - 14 taş, 14 konuşma; hikâye YALNIZ taşlarda anlatılır. Her konuşma oyunda bir kez oynar, ilerleme run'lar arasında kalıcı:
//   tek run'da R50'ye gelen bütün hikâyeyi o run'da görür; erken kaybeden, sonraki run'da yeni bir taşa gelince kaldığı yerden sürer.
// - Sıra sabittir (StoryOrder). Taş kazanılınca kayda yazılır (milestones); kazanılmış en ileri taşa kadar, görülmemiş ilk konuşma
//   sıradaki olur. Böylece konuşma atlanmaz ve sıra bozulmaz (ör. hiç ödül sunulmayan Sınama sonrası "ilk ödül" taşı geride
//   kalsa da sonraki taşla birlikte sırasıyla oynar).
// - Eri'nin hâli (aşama 0–3) taşın anında değişir: R25 konuşması açılırken 2 (gri, üzgün), zafer itirafının ilgili satırında 3
//   (kırmızı). Aşama yalnız ileri gider. 1 kullanılmıyor (eski plandan; ilk Sınama'da 1 yazılır, görünüş mutludur).
public static class DialogueProgress
{
    public const int MaxStage = 3, Sad = 2, Revealed = 3;

    // Hikâye konuşmaları ve taşları (aynı kimlik): oyundaki doğal sıra. Kimlikler kayıtta tutulur: yayınlandıktan sonra değişmez.
    // İlk Sınama takvimde R3'tedir (R5'e ulaşmak için geçilmesi gerekir), ödülü hemen ardından gelir.
    public const string TrialPassed = "story.trial1", FirstReward = "story.reward1", Victory = "story.victory", AfterVictory = "story.after";
    public static readonly string[] StoryOrder =
    {
        TrialPassed, FirstReward, "story.r5", "story.r10", "story.r15", "story.r20", "story.r25", "story.r30", "story.r35",
        "story.r40", "story.r45", "story.r50", Victory, AfterVictory,
    };
    // Round taşları: konuşma o round'un hazırlık ekranında (round'dan önceki sakin anda) çıkar.
    public static readonly (string id, int round)[] RoundBeats =
    {
        ("story.r5", 5), ("story.r10", 10), ("story.r15", 15), ("story.r20", 20), ("story.r25", 25), ("story.r30", 30),
        ("story.r35", 35), ("story.r40", 40), ("story.r45", 45), ("story.r50", 50),
    };
    // Bu konuşma açılırken Eri griye döner (üzgün aşama).
    public const string SadBeat = "story.r25";

    private static readonly HashSet<string> seen = new();
    private static MetaSaveData cached;

    // Eri'nin hâli değişti (görünüş, arayüz rengi bunu dinler).
    public static event Action StageChanged;

    private static MetaSaveData Data
    {
        get
        {
            MetaSaveData data = MetaSave.Data;
            if (!ReferenceEquals(data, cached))
            {
                cached = data;
                seen.Clear();
                foreach (string id in data.seenDialogue) seen.Add(id);
            }
            return data;
        }
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { seen.Clear(); cached = null; StageChanged = null; }

    public static int Stage => Data.storyStage;
    public static int RunsStarted => Data.runsStarted;
    public static bool IntroSeen => Data.introSeen;
    public static IReadOnlyList<string> PendingStory => Data.pendingStory;

    public static bool Seen(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        _ = Data;   // kayıt yeniden yüklendiyse küme yenilenir
        return seen.Contains(id);
    }

    public static void MarkSeen(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        MetaSaveData data = Data;
        if (!seen.Add(id)) return;
        data.seenDialogue.Add(id);
        data.pendingStory.Remove(id);
        MetaSave.MarkDirty();
    }

    public static void RunStarted()
    {
        Data.runsStarted++;
        MetaSave.MarkDirty();
    }

    // ---- hikâye taşları ----
    public static bool HasMilestone(string milestone) => !string.IsNullOrEmpty(milestone) && Data.milestones.Contains(milestone);

    // Taş kazanıldı (bir kez; hemen yazılır: kapanan oyun taşı kaybetmez). Döndürür: yeni mi.
    public static bool AddMilestone(string milestone)
    {
        MetaSaveData data = Data;
        if (string.IsNullOrEmpty(milestone) || data.milestones.Contains(milestone)) return false;
        data.milestones.Add(milestone);
        MetaSave.Save();
        return true;
    }

    // Kazanılmış en ileri taşın sırası (StoryOrder'da; hiçbiri yoksa -1).
    public static int StoryReach
    {
        get
        {
            List<string> milestones = Data.milestones;
            for (int i = StoryOrder.Length - 1; i >= 0; i--)
                if (milestones.Contains(StoryOrder[i])) return i;
            return -1;
        }
    }

    // Sıradaki hikâye konuşması: kazanılmış en ileri taşa kadar görülmemiş ilk konuşma (yoksa null). Atlanmaz, sıra bozulmaz.
    public static string NextStory()
    {
        int reach = StoryReach;
        for (int i = 0; i <= reach; i++)
            if (!Seen(StoryOrder[i])) return StoryOrder[i];
        return null;
    }

    // Eri'nin hâli: yalnız ileri gider, hemen yazılır, haber verilir.
    public static void AdvanceStage(int stage)
    {
        MetaSaveData data = Data;
        stage = Math.Min(stage, MaxStage);
        if (stage <= data.storyStage) return;
        data.storyStage = stage;
        MetaSave.Save();
        StageChanged?.Invoke();
    }

    // Cevap seçimi (kalıcı): "konuşma:A" / "konuşma:B". Aynı konuşmanın seçimi değişmez (ilk seçim kalır).
    public static string Choice(string dialogueId)
    {
        if (string.IsNullOrEmpty(dialogueId)) return null;
        string prefix = dialogueId + ":";
        foreach (string c in Data.storyChoices)
            if (c.StartsWith(prefix, StringComparison.Ordinal)) return c.Substring(prefix.Length);
        return null;
    }

    public static void SaveChoice(string dialogueId, string choice)
    {
        if (string.IsNullOrEmpty(dialogueId) || string.IsNullOrEmpty(choice) || Choice(dialogueId) != null) return;
        Data.storyChoices.Add(dialogueId + ":" + choice);
        MetaSave.Save();
    }

    // "story.trial1:A" biçimindeki koşul kayıtla uyuşuyor mu (boş koşul: her zaman).
    public static bool ChoiceMatches(string condition)
    {
        if (string.IsNullOrEmpty(condition)) return true;
        int colon = condition.LastIndexOf(':');
        return colon > 0 && Choice(condition.Substring(0, colon)) == condition.Substring(colon + 1);
    }

    // Run sonu: yalnız sayaç (aşama artık taşın anında değişir).
    public static void RunFinished()
    {
        Data.runsFinished++;
        MetaSave.MarkDirty();
    }

    // Öğretim ile çakışan hikâye konuşması kaybolmaz: sonraki sakin ana ertelenir (kalıcı).
    public static void DeferStory(string id)
    {
        MetaSaveData data = Data;
        if (string.IsNullOrEmpty(id) || seen.Contains(id) || data.pendingStory.Contains(id)) return;
        data.pendingStory.Add(id);
        MetaSave.MarkDirty();
    }

    public static void MarkIntroSeen()
    {
        if (Data.introSeen) return;
        Data.introSeen = true;
        MetaSave.Save();
    }

#if UNITY_EDITOR
    // ---- Mechanics Test Lab (Faz 6, 5 Kasım 2026): yalnız editörde ----
    // Lab bunları yalnız kaydın bellekte tutulduğu test oturumunda çağırır (MetaSave.EditorSessionMemoryOnly): diske yazılmaz.
    // Oyunun kuralından farklı olarak aşama geri alınabilir, cevap değiştirilebilir.
    public static void EditorSetStage(int stage)
    {
        MetaSaveData data = Data;
        stage = Math.Clamp(stage, 0, MaxStage);
        if (data.storyStage == stage) return;
        data.storyStage = stage;
        MetaSave.MarkDirty();
        StageChanged?.Invoke();
    }

    public static void EditorSetMilestone(string milestone, bool reached)
    {
        MetaSaveData data = Data;
        if (string.IsNullOrEmpty(milestone)) return;
        if (!reached) data.milestones.Remove(milestone);
        else if (!data.milestones.Contains(milestone)) data.milestones.Add(milestone);
        MetaSave.MarkDirty();
    }

    public static void EditorSetSeen(string id, bool value)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (value) { MarkSeen(id); return; }
        MetaSaveData data = Data;
        seen.Remove(id);
        data.seenDialogue.Remove(id);
        MetaSave.MarkDirty();
    }

    // Kayıtlı cevabı değiştirir ya da siler (choice boş).
    public static void EditorSetChoice(string dialogueId, string choice)
    {
        if (string.IsNullOrEmpty(dialogueId)) return;
        string prefix = dialogueId + ":";
        MetaSaveData data = Data;
        data.storyChoices.RemoveAll(c => c.StartsWith(prefix, StringComparison.Ordinal));
        if (!string.IsNullOrEmpty(choice)) data.storyChoices.Add(prefix + choice);
        MetaSave.MarkDirty();
    }

    // Görülen konuşmalar ve ertelenen hikâye silinir: öğretimler ve tepkiler yeniden çıkar.
    public static void EditorResetSeen()
    {
        MetaSaveData data = Data;
        data.seenDialogue.Clear();
        data.pendingStory.Clear();
        seen.Clear();
        MetaSave.MarkDirty();
    }

    // Hikâye baştan: taşlar, cevaplar, hikâye konuşmalarının görülmesi silinir, aşama 0 (öğretimlere dokunmaz).
    public static void EditorResetStory()
    {
        MetaSaveData data = Data;
        foreach (string id in StoryOrder)
        {
            data.milestones.Remove(id);
            data.seenDialogue.Remove(id);
            data.pendingStory.Remove(id);
            seen.Remove(id);
        }
        data.storyChoices.Clear();
        MetaSave.MarkDirty();
        EditorSetStage(0);
    }
#endif
}
