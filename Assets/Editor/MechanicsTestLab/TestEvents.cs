using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MechanicsTestLab
{
    // Boss ve tarla olayı komutları. Kurallar oyununkidir:
    // - Boss: seçilen boss, oturumun ÇALIŞMA ZAMANI profil kopyasının takvimine o tarih için yazılır (kaynak profil asset'i
    //   değişmez); boss ritmi baştan kurulur ve round oyunun kendi geçişiyle başlar. Boss'u yürütücü (SegmentEventDirector)
    //   hazırlar, etkinleştirir ve temizler.
    // - Tarla olayı: oyunun yürütücüsüyle başlar (FieldEventDirector.StartNow). Bölge olayın kendi seçim kodundan çıkar; istenen
    //   hücre için seed'ler önizlenir (EditorPreview) ve yalnız o hücreyi içeren geçerli bir bölge varsa olay başlar. Bölge başka
    //   yere kaydırılmaz. Aynı anda tek olay kuralı sürer (yeni olay öncekini oyunun temizlik yoluyla kapatır).
    public static class TestEvents
    {
        public static readonly string[] EventNames = { "Diken Kuşatması", "İstilacı Filiz", "Aşılı Kabuk", "Gezgin Bereket" };
        private const int SeedSearch = 600;

        // ---------------------------------------------------------------- boss
        public static Result StartBoss(SegmentEventSO boss, int round, bool ignoreEligibility = false)
        {
            RoundManager rounds = RoundManager.Instance;
            SegmentEventDirector director = SegmentEventDirector.Instance;
            RunProfileSO profile = TestSession.Profile;
            if (rounds == null || director == null || profile == null) return Result.Fail("Round yöneticisi ya da boss yürütücüsü yok.");
            if (boss == null) return Result.Fail("Boss seçilmedi.");
            if (!rounds.Calendar.IsExplicit) return Result.Fail("Bu profilin açık boss takvimi yok: boss tarihe atanamaz.");
            if (!rounds.IsBossRound(round)) return Result.Fail($"R{round} bir boss round'u değil. Boss round'ları: {TestRewards.BossRounds(rounds)}.");
            if (!ignoreEligibility && !director.EditorIsEligible(boss))
                return Result.Fail($"{boss.displayName} bu tarla ve oyuncu durumunda havuz kuralına göre seçilemez (normal oyunda oluşmaz). " +
                                   "Yine de denemek için 'uygunluğu yok say' seçeneğini aç.");
            int period = rounds.Calendar.PeriodOf(round);
            BossDate date = profile.bossCalendar[period - 1];
            date.boss = boss;
            profile.bossCalendar[period - 1] = date;
            director.EditorRestartBossRhythm();
            BossRewardManager.Instance?.EditorAllowOfferAgain();
            if (!rounds.EditorJumpToRound(round)) return Result.Fail($"R{round} başlatılamadı.");
            SegmentEventRuntime active = director.Active;
            if (active == null || active.Data != boss)
                return Result.Fail($"R{round} başladı ama {boss.displayName} aktif değil (aktif: {(active != null ? active.Data.displayName : "yok")}).");
            TestLog.Note(TestLog.Kind.Event, $"Boss başladı: {boss.displayName} · R{round} · {active.RuleSummary}");
            return Result.Success($"{boss.displayName} R{round}'da aktif: {active.RuleSummary}. Round sonunda oyunun kendi yoluyla biter.");
        }

        // Aktif boss'un etkisini oyunun temizlik yoluyla kaldırır; tarla, ödüller ve round aynen sürer.
        public static Result ClearBoss()
        {
            SegmentEventDirector director = SegmentEventDirector.Instance;
            if (director == null) return Result.Fail("Boss yürütücüsü yok.");
            bool had = director.Active != null;
            director.EditorRestartBossRhythm();
            return Result.Success(had ? "Boss etkisi temizlendi (ritim baştan kuruldu; bu round'da boss yeniden etkinleşmez)." : "Aktif boss yoktu; ritim baştan kuruldu.");
        }

        public static string BossStatus()
        {
            SegmentEventDirector director = SegmentEventDirector.Instance;
            if (director == null) return "Boss yürütücüsü yok.";
            var text = new StringBuilder();
            SegmentEventRuntime active = director.Active, upcoming = director.Upcoming;
            text.Append("Aktif boss: ").Append(active != null ? $"{active.Data.displayName} · {active.RuleSummary} · {active.ZoneCells.Count} hücre" : "yok");
            if (upcoming != null) text.Append("\nDuyurulan: ").Append(upcoming.Data.displayName).Append(" · R").Append(upcoming.StartRound);
            if (!string.IsNullOrEmpty(director.LastPickNote)) text.Append("\nSeçim notu: ").Append(director.LastPickNote);
            return text.ToString();
        }

        // ---------------------------------------------------------------- tarla olayı
        // anchor: bölgenin içermesi istenen hücre (null: seed'in seçtiği bölge).
        public static Result StartEvent(FieldEventKind kind, float delay, int seed, GridPosition? anchor = null)
        {
            FieldEventDirector director = FieldEventDirector.Instance;
            RoundManager rounds = RoundManager.Instance;
            if (director == null || rounds == null) return Result.Fail("Tarla olayı yürütücüsü yok.");
            FieldEventPlanSO plan = director.Plan;
            string name = EventNames[(int)kind];
            if (plan == null) return Result.Fail("Bu profilin tarla olayı planı yok (olaylar bu profilde kapalı).");
            if (director.PlanError != null) return Result.Fail("Tarla olayı planı geçersiz: " + director.PlanError);
            if (!rounds.IsRoundActive || !TestTime.InRound) return Result.Fail("Tarla olayı yalnız aktif round'da başlar.");
            if (anchor.HasValue)
            {
                GridPosition wanted = anchor.Value;
                if (!FieldCells.IsOpen(FieldCells.Grid, wanted)) return Result.Fail($"({wanted.x}, {wanted.z}) açık bir tarla hücresi değil; olay başlatılmadı.");
                int found = -1;
                for (int candidate = seed; candidate < seed + SeedSearch && found < 0; candidate++)
                {
                    FieldEvent preview = director.EditorPreview(kind, candidate, plan);
                    if (preview == null) break;   // uygun bölge hiç yok: seed değiştirmek sonucu değiştirmez
                    if (Covers(preview, kind, wanted)) found = candidate;
                }
                if (found < 0)
                    return Result.Fail($"{name}: ({wanted.x}, {wanted.z}) hücresini içeren geçerli bir bölge bulunamadı (olayın alan kuralları: " +
                                       "açık alan, üretim noktası payı, boyut). Bölge başka yere taşınmadı; olay başlatılmadı.");
                seed = found;
            }
            if (!director.StartNow(kind, delay, seed, plan))
                return Result.Fail($"{name} kurulamadı: uygun bölge yok (ör. üretim noktası yok ya da bölge bütün üretimi kaplardı).");
            string note = rounds.CurrentRound < plan.firstRound ? $" Not: normal takvimde olaylar R{plan.firstRound}'dan önce çıkmaz." : "";
            TestLog.Note(TestLog.Kind.Event, $"Tarla olayı kuruldu: {name} · seed {seed} · {Zones(director.Current)}");
            return Result.Success($"{name} kuruldu (seed {seed}; hazırlık {delay:0.#} sn sonra) · {Zones(director.Current)}.{note}");
        }

        // İstilacı Filiz'de "bölge" ana filizin hücresidir; diğerlerinde bölgelerden biri hücreyi içermelidir.
        private static bool Covers(FieldEvent preview, FieldEventKind kind, GridPosition cell)
        {
            if (kind == FieldEventKind.InvasiveSprout)
                return preview.Targets.Count > 0 && preview.Targets[0].Cell.x == cell.x && preview.Targets[0].Cell.z == cell.z;
            for (int zone = 0; zone < preview.ZoneCount; zone++)
            {
                IReadOnlyList<GridPosition> cells = preview.Zone(zone);
                for (int i = 0; i < cells.Count; i++) if (cells[i].x == cell.x && cells[i].z == cell.z) return true;
            }
            return false;
        }

        public static Result EndEvent()
        {
            FieldEventDirector director = FieldEventDirector.Instance;
            if (director == null) return Result.Fail("Tarla olayı yürütücüsü yok.");
            bool had = director.Current != null;
            director.End();
            return Result.Success(had ? "Tarla olayı oyunun temizlik yoluyla kapatıldı." : "Aktif tarla olayı yoktu.");
        }

        public static string Zones(FieldEvent current)
        {
            if (current == null) return "olay yok";
            var text = new StringBuilder();
            for (int zone = 0; zone < current.ZoneCount; zone++)
            {
                IReadOnlyList<GridPosition> cells = current.Zone(zone);
                if (cells.Count == 0) continue;
                int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
                foreach (GridPosition cell in cells)
                {
                    minX = Mathf.Min(minX, cell.x); maxX = Mathf.Max(maxX, cell.x);
                    minZ = Mathf.Min(minZ, cell.z); maxZ = Mathf.Max(maxZ, cell.z);
                }
                text.Append(text.Length > 0 ? " · " : "").Append($"bölge {zone}: ({minX}, {minZ})–({maxX}, {maxZ})");
            }
            for (int i = 0; i < current.Targets.Count; i++)
                text.Append(text.Length > 0 ? " · " : "").Append($"hedef {i}: ({current.Targets[i].Cell.x}, {current.Targets[i].Cell.z})");
            return text.Length > 0 ? text.ToString() : "bölge yok";
        }

        public static string EventStatus()
        {
            FieldEventDirector director = FieldEventDirector.Instance;
            if (director == null) return "Tarla olayı yürütücüsü yok.";
            FieldEvent current = director.Current;
            if (current == null) return $"Aktif tarla olayı yok · başlatılan {director.Started} · kurulamayan {director.Skipped}";
            var text = new StringBuilder();
            text.Append(current.Title).Append(" · faz ").Append(current.Phase).Append(" · ").Append(current.PhaseTime.ToString("0.0")).Append(" sn");
            if (current.Phase == FieldEventPhase.Active && !float.IsInfinity(current.ActiveRemaining)) text.Append(" · kalan ").Append(current.ActiveRemaining.ToString("0.0")).Append(" sn");
            text.Append('\n').Append(current.Status).Append('\n');
            for (int zone = 0; zone < current.ZoneCount; zone++)
                text.Append("bölge ").Append(zone).Append(": ").Append(current.Zone(zone).Count).Append(" hücre").Append(current.ZoneClosed(zone) ? " · ÜRETİME KAPALI" : "").Append('\n');
            for (int i = 0; i < current.Targets.Count; i++)
            {
                InterventionTarget target = current.Targets[i];
                text.Append("hedef ").Append(i).Append(" (").Append(target.Cell.x).Append(", ").Append(target.Cell.z).Append("): ").Append(target.Remaining).Append('/').Append(target.Durability)
                    .Append(target.Broken ? " · kırıldı" : target.Hittable ? " · vurulabilir (yalnız doğrudan saldırı)" : " · henüz vurulamaz").Append('\n');
            }
            text.Append("üretimi kapalı hücre ").Append(director.Zones.BlockedCells).Append(" · yavaşlamış hücre ").Append(director.Zones.SlowedCells);
            return text.ToString();
        }
    }
}
