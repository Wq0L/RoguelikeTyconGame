using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MechanicsTestLab
{
    // Diyalog / tutorial / hikâye komutları (Faz 6, 5 Kasım 2026). Kurallar:
    // - Lab oturumunda diyalog yöneticisi kendiliğinden konuşmaz (DialogueDirector.Suspended). "Yönetici açık" seçeneği
    //   (DialogueDirector.LabEnabled) oyunun normal tetiklerini bu oturumda açar: öğretimler ve hikâye oyundaki gibi çıkar.
    // - Konuşma oynatma koşullara bakmaz (aşama, görüldü, ekran); kendi kutusunda açılır. "Görüldü say" kapalıysa kayıt değişmez.
    // - Kayıt komutları (aşama, taş, cevap, sıfırlama) yalnız kaydın bellekte tutulduğu test oturumunda çalışır: gerçek oyuncu
    //   kaydına hiçbir şey yazılmaz. Oturum yeniden kurulsa da bellekteki ilerleme kalır (Play bitince gider).
    public static class TestDialogue
    {
        public static readonly string[] StageNames = { "0 · başlangıç (mutlu)", "1 · ilk Sınama geçildi (mutlu)", "2 · üzgün (gri, R25)", "3 · kırmızı (zafer itirafı)" };

        public static DialogueDirector Director => DialogueDirector.Instance;

        private static Result Guard()
        {
            if (!TestSession.Ready) return Result.Fail("Test oturumu hazır değil.");
            if (Director == null) return Result.Fail("Diyalog yöneticisi yok.");
            return Result.Success("");
        }

        private static Result SaveGuard()
        {
            Result g = Guard();
            if (!g.Ok) return g;
            if (!MetaSave.EditorSessionMemoryOnly) return Result.Fail("Kayıt bu oturumda bellekte değil: gerçek kayda yazmamak için komut reddedildi.");
            return g;
        }

        // ---------------------------------------------------------------- yönetici
        public static bool Enabled => DialogueDirector.LabEnabled;

        public static Result SetEnabled(bool on)
        {
            Result g = Guard();
            if (!g.Ok) return g;
            DialogueDirector.LabEnabled = on;
            return Result.Success(on ? "Diyalog yöneticisi bu oturumda AÇIK: öğretimler ve hikâye oyundaki tetiklerle çıkar."
                                     : "Diyalog yöneticisi bu oturumda KAPALI: konuşmalar yalnız bu sekmeden oynatılır.");
        }

        // Oyunun tetiğini şimdi yayınlar: yönetici oyundaki gibi uygun konuşmayı seçer (aşama, görüldü, ekran kuralları geçerli).
        public static Result Raise(DialogueTrigger trigger)
        {
            Result g = Guard();
            if (!g.Ok) return g;
            if (!Enabled) return Result.Fail("Tetik yayınlamak için önce yöneticiyi aç (oyunun kurallarıyla seçim yapar).");
            DialogueEntry before = Director.Current;
            Director.Raise(trigger);
            DialogueEntry now = Director.Current;
            if (now != null && now != before) return Result.Success($"{trigger}: '{now.id}' açıldı.");
            if (Director.Queued != null) return Result.Success($"{trigger}: '{Director.Queued.id}' sırada bekliyor (açık kutu bitince).");
            return Result.Success($"{trigger}: uygun konuşma yok (görülmüş, aşaması uymuyor ya da bu ekranın kutusu değil: büyük kutu round dışında, küçük kutu round içinde).");
        }

        // ---------------------------------------------------------------- oynatma
        // line < 0: bütün konuşma. line ≥ 0: yalnız o satır (kayıtlı cevaba bağlı koşulu yok sayılır).
        public static Result Play(DialogueEntry entry, int line, bool markSeen)
        {
            Result g = Guard();
            if (!g.Ok) return g;
            if (entry == null || entry.lines == null || entry.lines.Length == 0) return Result.Fail("Konuşma boş.");
            if (markSeen && !MetaSave.EditorSessionMemoryOnly) return Result.Fail("Kayıt bellekte değil: 'görüldü say' kapalıyken oynat.");
            DialogueEntry shown = entry;
            if (line >= 0)
            {
                if (line >= entry.lines.Length) return Result.Fail("Satır yok.");
                shown = SingleLine(entry, line);
                markSeen = false;
            }
            Director.EditorPlay(shown, markSeen);
            string where = entry.size == DialogueSize.Planet ? "gezegendeki Eri'nin balonunda" : entry.size == DialogueSize.Small ? "küçük kutuda" : "büyük kutuda";
            return Result.Success((line >= 0 ? $"'{entry.id}' satır {line + 1}" : $"'{entry.id}'") + $" {where} oynatılıyor" + (markSeen ? " (görüldü sayıldı)." : " (kayıt değişmedi)."));
        }

        // Tek satırlık kopya: kimlik aynı (cevap kaydı o konuşmaya yazılır), cevaba bağlı koşul kaldırılır.
        private static DialogueEntry SingleLine(DialogueEntry entry, int line)
        {
            var copy = JsonUtility.FromJson<DialogueLine>(JsonUtility.ToJson(entry.lines[line]));
            copy.onlyIfChoice = "";
            return new DialogueEntry
            {
                id = entry.id, category = entry.category, trigger = entry.trigger, size = entry.size, frequency = DialogueFrequency.Repeat,
                minStage = 0, maxStage = DialogueProgress.MaxStage, lines = new[] { copy },
            };
        }

        public static Result CloseAll()
        {
            Result g = Guard();
            if (!g.Ok) return g;
            Director.EditorCloseAll();
            return Result.Success("Açık kutular kapandı (sıradaki konuşma açılmadı).");
        }

        // ---------------------------------------------------------------- hikâye
        public static Result SetStage(int stage)
        {
            Result g = SaveGuard();
            if (!g.Ok) return g;
            DialogueProgress.EditorSetStage(stage);
            return Result.Success($"Hikâye aşaması {DialogueProgress.Stage}: Eri {MascotFrames.MoodName}, konuşma kutusu grisi {UiMood.Applied:0.00}.");
        }

        // Kazanılmamış ilk taş kazanılır (oyundaki sırayla); sakin ekrandaysan (hazırlık / round sonu / run sonu) konuşması açılır.
        public static Result WinNextStone()
        {
            Result g = SaveGuard();
            if (!g.Ok) return g;
            string id = null;
            foreach (string s in DialogueProgress.StoryOrder)
                if (!DialogueProgress.HasMilestone(s)) { id = s; break; }
            if (id == null) return Result.Fail("Bütün taşlar zaten kazanılmış.");
            DialogueProgress.AddMilestone(id);
            if (!Enabled) return Result.Success($"'{id}' taşı kazanıldı. Konuşma için yöneticiyi aç ya da 'sıradaki hikâyeyi oynat'.");
            Director.ResetStoryMoment();
            Director.ForceStoryCheck();
            return Result.Success($"'{id}' taşı kazanıldı." + (Director.Current != null ? $" Açık: '{Director.Current.id}'." : " Konuşma sakin bir ekranda (hazırlık / round sonu) açılır."));
        }

        // Sıradaki hikâye konuşması oyundaki yoldan açılmış gibi: aşama geçişi (Taş 1 → 1, R25 → 2) yapılır, görüldü sayılır.
        public static Result PlayNextStory()
        {
            Result g = SaveGuard();
            if (!g.Ok) return g;
            string id = DialogueProgress.NextStory();
            if (id == null) return Result.Fail("Sıradaki hikâye yok: önce bir taş kazan.");
            if (!Director.EditorTryGet(id, out DialogueEntry e)) return Result.Fail($"'{id}' katalogda yok.");
            if (id == DialogueProgress.TrialPassed) DialogueProgress.AdvanceStage(1);
            if (id == DialogueProgress.SadBeat) DialogueProgress.AdvanceStage(DialogueProgress.Sad);
            Director.EditorPlay(e, true);
            return Result.Success($"'{id}' oynatılıyor (görüldü sayıldı) · aşama {DialogueProgress.Stage}.");
        }

        public static Result SetStone(string id, bool reached)
        {
            Result g = SaveGuard();
            if (!g.Ok) return g;
            DialogueProgress.EditorSetMilestone(id, reached);
            if (!reached) DialogueProgress.EditorSetSeen(id, false);
            return Result.Success(reached ? $"'{id}' taşı kazanıldı sayıldı." : $"'{id}' taşı ve konuşmasının görülmesi silindi.");
        }

        // Taş 1 cevabı: "A", "B" ya da null (silinir).
        public static Result SetTrialChoice(string choice)
        {
            Result g = SaveGuard();
            if (!g.Ok) return g;
            DialogueProgress.EditorSetChoice(DialogueProgress.TrialPassed, choice);
            return Result.Success("Taş 1 cevabı: " + (choice ?? "yok") + " (zafer itirafında buna bağlı satır değişir).");
        }

        // ---------------------------------------------------------------- sıfırlama
        public static Result ResetSeen()
        {
            Result g = SaveGuard();
            if (!g.Ok) return g;
            DialogueProgress.EditorResetSeen();
            Director.ResetStoryMoment();
            return Result.Success("Görülen konuşmalar silindi: öğretimler ve tepkiler yeniden çıkar (yönetici açıkken, kendi tetikleriyle).");
        }

        public static Result ResetStory()
        {
            Result g = SaveGuard();
            if (!g.Ok) return g;
            DialogueProgress.EditorResetStory();
            Director.ResetStoryMoment();
            return Result.Success("Hikâye baştan: taşlar, cevaplar ve hikâye konuşmaları silindi, aşama 0.");
        }

        // ---------------------------------------------------------------- durum
        public static int SeenCount(IReadOnlyList<DialogueEntry> entries)
        {
            int n = 0;
            foreach (DialogueEntry e in entries) if (e != null && DialogueProgress.Seen(e.id)) n++;
            return n;
        }

        public static string StoryState(string id) =>
            DialogueProgress.Seen(id) ? "görüldü" : DialogueProgress.HasMilestone(id) ? "kazanıldı, bekliyor" : "—";

        public static string Status()
        {
            var b = new StringBuilder();
            DialogueDirector d = Director;
            b.Append($"Aşama {DialogueProgress.Stage} · Eri {MascotFrames.MoodName} · kutu grisi {UiMood.Applied:0.00}");
            b.Append($" · Taş 1 cevabı {DialogueProgress.Choice(DialogueProgress.TrialPassed) ?? "yok"}");
            b.Append($" · sıradaki hikâye {DialogueProgress.NextStory() ?? "yok"}");
            if (d != null)
            {
                b.Append($"\nYönetici {(Enabled ? "AÇIK" : "kapalı")} · ekran {d.State} · açık {(d.Current != null ? d.Current.id : "yok")}");
                b.Append($" · bekleyen {(d.Queued != null ? d.Queued.id : "yok")} · ertelenen hikâye {DialogueProgress.PendingStory.Count}");
                b.Append($" · görülen {SeenCount(d.EditorEntries)}/{d.EditorEntries.Count}");
            }
            b.Append(MetaSave.EditorSessionMemoryOnly ? "\nKayıt: yalnız bellekte (gerçek kayda yazılmaz)." : "\nKayıt bellekte DEĞİL: kayıt komutları kapalı.");
            return b.ToString();
        }
    }
}
