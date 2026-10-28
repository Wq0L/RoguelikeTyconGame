using System.Collections.Generic;

// Diyalog ve hikâye ilerlemesinin kalıcı kaydı (MetaSave.Data üzerinde; ayrı dosya yok).
// - Görülen konuşmalar: oyun içinde kümeye çevrilir (hızlı arama); kayıt nesnesi değişirse (yeniden yükleme) küme yenilenir.
// - Satır görülünce diske hemen yazılmaz (MarkDirty); hikâye aşaması ve giriş videosu gibi önemli anlar hemen yazılır.
// - Hikâye aşaması (0–3) yalnız run sonunda kazanılmış kilometre taşlarından hesaplanır (run içinde ton değişmez).
public static class DialogueProgress
{
    public const int MaxStage = 3;
    // Kilometre taşları ve açtıkları aşama. Aşama 3 = tam oyunun zaferi (demo zaferi bu taşı açmaz).
    public const string FirstBoss = "stage1.firstBoss", Round25 = "stage2.round25", FullVictory = "stage3.fullVictory";

    private static readonly HashSet<string> seen = new();
    private static MetaSaveData cached;

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
    private static void ResetStatics() { seen.Clear(); cached = null; }

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

    // Kilometre taşı run içinde işaretlenir; aşama run sonunda uygulanır.
    public static void AddMilestone(string milestone)
    {
        MetaSaveData data = Data;
        if (string.IsNullOrEmpty(milestone) || data.milestones.Contains(milestone)) return;
        data.milestones.Add(milestone);
        MetaSave.MarkDirty();
    }

    // Run sonu: sayaç, kazanılmış en yüksek aşama. Aşama arttıysa hemen yazılır. Döndürür: aşama değişti mi.
    public static bool RunFinished()
    {
        MetaSaveData data = Data;
        data.runsFinished++;
        int stage = data.storyStage;
        if (data.milestones.Contains(FirstBoss)) stage = System.Math.Max(stage, 1);
        if (data.milestones.Contains(Round25)) stage = System.Math.Max(stage, 2);
        if (data.milestones.Contains(FullVictory)) stage = System.Math.Max(stage, 3);
        bool changed = stage != data.storyStage;
        data.storyStage = System.Math.Min(stage, MaxStage);
        if (changed) MetaSave.Save(); else MetaSave.MarkDirty();
        return changed;
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
}
