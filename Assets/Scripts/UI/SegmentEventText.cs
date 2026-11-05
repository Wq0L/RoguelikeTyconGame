using UnityEngine;

// Segment olaylarının (boss) arayüz metinleri: HUD satırı, round özeti bildirimi ve round intro alt yazısı.
// Sadece okur; kural SegmentEventDirector ve olayın kendisindedir.
// Boss ritminde (olay yalnız segmentin son round'unda aktif) metinler boss hasadı hedefini de söyler; eski profillerin metinleri aynıdır.
// Yazılar çeviri tablosundan (ui.event.*), boss adı verideki çeviriden (SegmentEventSO.DisplayName). Her cümle tek anahtar,
// değişkenler yer tutucuyla; " · " ile ayrılan bölümler ve satırlar kodda birleşir.
public static class SegmentEventText
{
    // Renkler boss kimliğinden gelir (BossTheme); burada sabit renk yoktur.

    // Round özetindeki bildirimin anlattığı boss: az önce biten, yoksa aktif, yoksa yaklaşan.
    public static SegmentEventSO NoticeSubject(SegmentEventDirector events, int finishedRound)
    {
        if (events == null) return null;
        if (events.LastEnded != null && events.LastEndedRound == finishedRound) return events.LastEnded.Data;
        return BossTheme.Current(events);
    }

    // Round girişi yazısının anlattığı boss: bu round başlayan, az önce biten ya da bir sonraki round gelecek olan.
    public static SegmentEventSO IntroSubject(SegmentEventDirector events, RoundManager rounds)
    {
        if (events == null || rounds == null) return null;
        int round = rounds.CurrentRound;
        if (events.Active != null && round == events.Active.StartRound) return events.Active.Data;
        if (events.LastEnded != null && round == events.LastEndedRound + 1) return events.LastEnded.Data;
        return events.Upcoming != null ? events.Upcoming.Data : null;
    }

    // "Mavi şerit: 3. satırda üretim ×1,5 yavaş" ya da bölgesiz olayda "Vuruş yarıçapı ×0,85".
    public static string Rule(SegmentEventRuntime e) =>
        e.ZoneHint != null ? $"{GameText.Capitalize(e.ZoneHint)}: {e.RuleSummary}" : GameText.Capitalize(e.RuleSummary);

    private static long BossTarget(SegmentEventRuntime e) =>
        RoundManager.Instance != null ? RoundManager.Instance.BossTargetFor(e.Segment) : 0;

    private static string Target(SegmentEventRuntime e)
    {
        long target = BossTarget(e);
        return target > 0 ? GameText.Format("ui.event.boss_target", HarvestQuota.Format(target)) : GameText.Get("ui.event.boss_target.none");
    }

    // Round sürerken HUD: aktif olay ya da yaklaşan olay.
    public static string HudLine(SegmentEventDirector events)
    {
        if (events == null) return null;
        if (events.Active != null) return $"{events.Active.Data.DisplayName}: {events.Active.RuleSummary}";
        SegmentEventRuntime next = events.Upcoming;
        if (next == null) return null;
        string zone = next.ZoneHint != null ? $" · {next.ZoneHint}" : "";
        return GameText.Format(next.BossRoundOnly ? "ui.event.upcoming_boss" : "ui.event.upcoming", next.Data.DisplayName, next.StartRound) + zone;
    }

    // Round özeti: finishedRound = biten round (0: ilk round'dan önceki hazırlık).
    public static string Notice(SegmentEventDirector events, int finishedRound)
    {
        if (events == null) return null;
        if (events.BossMode) return BossNotice(events, finishedRound);
        SegmentEventRuntime active = events.Active;
        if (active != null && finishedRound < active.EndRound)
            return GameText.Format("ui.event.active", active.Data.DisplayName, active.EndRound - finishedRound) + "\n" +
                   $"<size=72%>{Rule(active)}</size>";
        if (events.LastEnded != null && events.LastEndedRound == finishedRound)
            return $"{GameText.Format("ui.event.passed", events.LastEnded.Data.DisplayName)}\n<size=72%>{events.LastEnded.EndedText}</size>";
        SegmentEventRuntime next = events.Upcoming;
        if (next == null || next.StartRound <= finishedRound) return null;
        int prep = next.StartRound - 1 - finishedRound;
        if (prep == 0)
            return GameText.Format("ui.event.final_prep", next.Data.DisplayName) + "\n" +
                   $"<size=72%>{GameText.Format("ui.event.round_range", next.StartRound, next.EndRound)} · {next.ZoneHint}: {next.RuleSummary}</size>";
        return GameText.Format("ui.event.upcoming_range", next.Data.DisplayName, next.StartRound, next.EndRound) + "\n" +
               $"<size=72%>{Rule(next)} · {GameText.Format("ui.event.prep_rounds", prep)}</size>";
    }

    // Boss ritmi: yaklaşan boss'un adı, kuralı ve boss round'undaki hedef; boss round'u bitince sonucu ve sıradaki boss.
    private static string BossNotice(SegmentEventDirector events, int finishedRound)
    {
        RoundManager rounds = RoundManager.Instance;
        SegmentEventRuntime next = events.Upcoming;
        if (events.LastEnded != null && events.LastEndedRound == finishedRound)
        {
            string result = rounds != null && rounds.LastBossRound == finishedRound && rounds.LastBossTarget > 0
                ? " · " + GameText.Format("ui.event.boss_harvest", HarvestQuota.Format(rounds.LastBossScore), HarvestQuota.Format(rounds.LastBossTarget)) : "";
            string after = next != null
                ? $"{GameText.Format("ui.event.next_boss", next.Data.DisplayName, next.StartRound)} · {Rule(next)} · {Target(next)}"
                : events.LastEnded.EndedText;
            return $"{GameText.Format("ui.event.passed", events.LastEnded.Data.DisplayName)}{result}\n<size=72%>{after}</size>";
        }
        if (next == null || next.StartRound <= finishedRound) return null;
        int prep = next.StartRound - 1 - finishedRound;
        return prep == 0
            ? $"{GameText.Format("ui.event.final_prep_boss", next.Data.DisplayName)}\n<size=72%>{Rule(next)} · {Target(next)}</size>"
            : $"{GameText.Format("ui.event.upcoming_boss", next.Data.DisplayName, next.StartRound)}\n" +
              $"<size=72%>{Rule(next)} · {Target(next)} · {GameText.Format("ui.event.prep_rounds", prep)}</size>";
    }

    // Round başı: olay başladı / bitti; olay yoksa null (kota metni kullanılır).
    public static string Intro(SegmentEventDirector events, RoundManager rounds)
    {
        if (events == null || rounds == null) return null;
        int round = rounds.CurrentRound;
        bool segmentStart = rounds.QuotaEnabled && rounds.Calendar.IsPeriodStart(round);
        string quota = segmentStart ? " · " + GameText.Format("ui.event.intro_quota", HarvestQuota.Format(rounds.QuotaTarget)) : "";
        if (events.Active != null && round == events.Active.StartRound)
        {
            if (!events.Active.BossRoundOnly) return GameText.Format("ui.event.started", events.Active.Data.DisplayName) + quota;
            long target = rounds.BossTarget;
            return target > 0
                ? GameText.Format("ui.event.boss_intro_target", events.Active.Data.DisplayName, HarvestQuota.Format(target))
                : GameText.Format("ui.event.boss_intro", events.Active.Data.DisplayName);
        }
        if (events.LastEnded != null && round == events.LastEndedRound + 1)
            return GameText.Format("ui.event.passed", events.LastEnded.Data.DisplayName) + quota;
        return null;
    }

    // Yaklaşan olayın son hazırlık round'u için intro (kota uyarısı yoksa gösterilir).
    public static string HeadsUp(SegmentEventDirector events, RoundManager rounds)
    {
        SegmentEventRuntime next = events != null ? events.Upcoming : null;
        return next != null && rounds != null && rounds.CurrentRound == next.StartRound - 1
            ? GameText.Format("ui.event.next_round", next.Data.DisplayName) : null;
    }

    // Eski profillerde olaylı segmentin kotası "boss kotası" diye adlandırılır. Boss ritminde her segmentte boss vardır ve
    // boss'un kendi hedefi ayrıdır; kota hep "segment kotası"dır.
    public static bool IsEventSegment(SegmentEventDirector events, int segment)
    {
        if (events == null || events.BossMode) return false;
        foreach (SegmentEventRuntime e in events.Events)
            if (e.Segment == segment) return true;
        return false;
    }
}
