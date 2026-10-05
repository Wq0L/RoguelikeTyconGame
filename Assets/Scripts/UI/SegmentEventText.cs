using UnityEngine;

// Segment olaylarının (boss) arayüz metinleri: HUD satırı, round özeti bildirimi ve round intro alt yazısı.
// Sadece okur; kural SegmentEventDirector ve olayın kendisindedir.
// Boss ritminde (olay yalnız segmentin son round'unda aktif) metinler boss hasadı hedefini de söyler; eski profillerin metinleri aynıdır.
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
        e.ZoneHint != null ? $"{Capitalize(e.ZoneHint)}: {e.RuleSummary}" : Capitalize(e.RuleSummary);

    private static string Capitalize(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0], System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) + text.Substring(1);

    private static long BossTarget(SegmentEventRuntime e) =>
        RoundManager.Instance != null ? RoundManager.Instance.BossTargetFor(e.Segment) : 0;

    private static string Target(SegmentEventRuntime e)
    {
        long target = BossTarget(e);
        return target > 0 ? $"boss hasadı hedefi {HarvestQuota.Format(target)}" : "boss hasadı hedefi yok";
    }

    // Round sürerken HUD: aktif olay ya da yaklaşan olay.
    public static string HudLine(SegmentEventDirector events)
    {
        if (events == null) return null;
        if (events.Active != null) return $"{events.Active.Data.displayName}: {events.Active.RuleSummary}";
        SegmentEventRuntime next = events.Upcoming;
        if (next == null) return null;
        string zone = next.ZoneHint != null ? $" · {next.ZoneHint}" : "";
        return next.BossRoundOnly
            ? $"Yaklaşan boss: {next.Data.displayName} · Round {next.StartRound}{zone}"
            : $"Yaklaşan: {next.Data.displayName} · Round {next.StartRound}{zone}";
    }

    // Round özeti: finishedRound = biten round (0: ilk round'dan önceki hazırlık).
    public static string Notice(SegmentEventDirector events, int finishedRound)
    {
        if (events == null) return null;
        if (events.BossMode) return BossNotice(events, finishedRound);
        SegmentEventRuntime active = events.Active;
        if (active != null && finishedRound < active.EndRound)
            return $"{active.Data.displayName} AKTİF · {active.EndRound - finishedRound} round kaldı\n" +
                   $"<size=72%>{Rule(active)}</size>";
        if (events.LastEnded != null && events.LastEndedRound == finishedRound)
            return $"{events.LastEnded.Data.displayName} GEÇTİ\n<size=72%>{events.LastEnded.EndedText}</size>";
        SegmentEventRuntime next = events.Upcoming;
        if (next == null || next.StartRound <= finishedRound) return null;
        int prep = next.StartRound - 1 - finishedRound;
        if (prep == 0)
            return $"SON HAZIRLIK · {next.Data.displayName} sonraki round\n" +
                   $"<size=72%>Round {next.StartRound}–{next.EndRound} · {next.ZoneHint}: {next.RuleSummary}</size>";
        return $"Yaklaşan: {next.Data.displayName} · Round {next.StartRound}–{next.EndRound}\n" +
               $"<size=72%>{Rule(next)} · {prep} round hazırlık</size>";
    }

    // Boss ritmi: yaklaşan boss'un adı, kuralı ve boss round'undaki hedef; boss round'u bitince sonucu ve sıradaki boss.
    private static string BossNotice(SegmentEventDirector events, int finishedRound)
    {
        RoundManager rounds = RoundManager.Instance;
        SegmentEventRuntime next = events.Upcoming;
        if (events.LastEnded != null && events.LastEndedRound == finishedRound)
        {
            string result = rounds != null && rounds.LastBossRound == finishedRound && rounds.LastBossTarget > 0
                ? $" · boss hasadı {HarvestQuota.Format(rounds.LastBossScore)} / {HarvestQuota.Format(rounds.LastBossTarget)}" : "";
            string after = next != null
                ? $"Sıradaki boss: {next.Data.displayName} (Round {next.StartRound}) · {Rule(next)} · {Target(next)}"
                : events.LastEnded.EndedText;
            return $"{events.LastEnded.Data.displayName} GEÇTİ{result}\n<size=72%>{after}</size>";
        }
        if (next == null || next.StartRound <= finishedRound) return null;
        int prep = next.StartRound - 1 - finishedRound;
        return prep == 0
            ? $"SON HAZIRLIK · boss {next.Data.displayName} sonraki round\n<size=72%>{Rule(next)} · {Target(next)}</size>"
            : $"Yaklaşan boss: {next.Data.displayName} · Round {next.StartRound}\n<size=72%>{Rule(next)} · {Target(next)} · {prep} round hazırlık</size>";
    }

    // Round başı: olay başladı / bitti; olay yoksa null (kota metni kullanılır).
    public static string Intro(SegmentEventDirector events, RoundManager rounds)
    {
        if (events == null || rounds == null) return null;
        int round = rounds.CurrentRound;
        bool segmentStart = rounds.QuotaEnabled && rounds.Calendar.IsPeriodStart(round);
        string quota = segmentStart ? $" · KOTA {HarvestQuota.Format(rounds.QuotaTarget)}" : "";
        if (events.Active != null && round == events.Active.StartRound)
        {
            if (!events.Active.BossRoundOnly) return $"{events.Active.Data.displayName} BAŞLADI{quota}";
            long target = rounds.BossTarget;
            return target > 0 ? $"BOSS: {events.Active.Data.displayName} · HASAT HEDEFİ {HarvestQuota.Format(target)}" : $"BOSS: {events.Active.Data.displayName}";
        }
        if (events.LastEnded != null && round == events.LastEndedRound + 1)
            return $"{events.LastEnded.Data.displayName} GEÇTİ{quota}";
        return null;
    }

    // Yaklaşan olayın son hazırlık round'u için intro (kota uyarısı yoksa gösterilir).
    public static string HeadsUp(SegmentEventDirector events, RoundManager rounds)
    {
        SegmentEventRuntime next = events != null ? events.Upcoming : null;
        return next != null && rounds != null && rounds.CurrentRound == next.StartRound - 1
            ? $"{next.Data.displayName} SONRAKİ ROUND" : null;
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
