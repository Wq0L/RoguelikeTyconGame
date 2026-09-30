using UnityEngine;

// Segment olaylarının (boss) arayüz metinleri: HUD satırı, round özeti bildirimi ve round intro alt yazısı.
// Sadece okur; kural SegmentEventDirector ve olayın kendisindedir.
public static class SegmentEventText
{
    public static readonly Color Ink = new Color32(30, 86, 140, 255);
    public static readonly Color IntroColor = new Color(0.62f, 0.86f, 1f);

    // Round sürerken HUD: aktif olay ya da yaklaşan olay.
    public static string HudLine(SegmentEventDirector events)
    {
        if (events == null) return null;
        if (events.Active != null) return $"{events.Active.Data.displayName}: {events.Active.RuleSummary}";
        SegmentEventRuntime next = events.Upcoming;
        return next != null ? $"Yaklaşan: {next.Data.displayName} · Round {next.StartRound} · mavi şerit" : null;
    }

    // Round özeti: finishedRound = biten round (0: ilk round'dan önceki hazırlık).
    public static string Notice(SegmentEventDirector events, int finishedRound)
    {
        if (events == null) return null;
        SegmentEventRuntime active = events.Active;
        if (active != null && finishedRound < active.EndRound)
            return $"{active.Data.displayName} AKTİF · {active.EndRound - finishedRound} round kaldı\n" +
                   $"<size=72%>Mavi şerit: {active.RuleSummary}</size>";
        if (events.LastEnded != null && events.LastEndedRound == finishedRound)
            return $"{events.LastEnded.Data.displayName} GEÇTİ\n<size=72%>Şeritteki üretim normale döndü</size>";
        SegmentEventRuntime next = events.Upcoming;
        if (next == null || next.StartRound <= finishedRound) return null;
        int prep = next.StartRound - 1 - finishedRound;
        if (prep == 0)
            return $"SON HAZIRLIK · {next.Data.displayName} sonraki round\n" +
                   $"<size=72%>Round {next.StartRound}–{next.EndRound} · mavi şerit: {next.RuleSummary}</size>";
        return $"Yaklaşan: {next.Data.displayName} · Round {next.StartRound}–{next.EndRound}\n" +
               $"<size=72%>Mavi şerit: {next.RuleSummary} · {prep} round hazırlık</size>";
    }

    // Round başı: olay başladı / bitti; olay yoksa null (kota metni kullanılır).
    public static string Intro(SegmentEventDirector events, RoundManager rounds)
    {
        if (events == null || rounds == null) return null;
        int round = rounds.CurrentRound;
        bool segmentStart = (round - 1) % rounds.QuotaSegmentRounds == 0 && rounds.QuotaEnabled;
        string quota = segmentStart ? $" · KOTA {HarvestQuota.Format(rounds.QuotaTarget)}" : "";
        if (events.Active != null && round == events.Active.StartRound)
            return $"{events.Active.Data.displayName} BAŞLADI{quota}";
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

    public static bool IsEventSegment(SegmentEventDirector events, int segment)
    {
        if (events == null) return false;
        foreach (SegmentEventRuntime e in events.Events)
            if (e.Segment == segment) return true;
        return false;
    }
}
