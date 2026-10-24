using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MechanicsTestLab
{
    // Önce / sonra karşılaştırmasının tuttuğu iki görüntü. Görüntüler sayı / metin kopyasıdır; oyun değişince birlikte değişmez.
    public static class Comparison
    {
        public static NumericSnapshot Before { get; private set; }
        public static NumericSnapshot After { get; private set; }
        public static List<DiffRow> Rows { get; private set; } = new();
        public static GridPosition? Cell { get; private set; }
        public static int Version { get; private set; }

        public static Result TakeBefore(GridPosition? cell, string title = "önce")
        {
            if (!TestSession.Ready) return Result.Fail("Test oturumu hazır değil.");
            Cell = cell;
            Before = Snapshots.Capture(cell, title);
            After = null;
            Rows = new List<DiffRow>();
            Version++;
            return Result.Success($"\"Önce\" görüntüsü alındı: {Before.Target} · {Before.Rows.Count} satır. Şimdi değişikliği yap, sonra karşılaştır.");
        }

        // "Sonra" görüntüsü aynı hedeften alınır ve fark tablosu kurulur.
        public static Result TakeAfter(bool onlyChanged, string title = "sonra")
        {
            if (!TestSession.Ready) return Result.Fail("Test oturumu hazır değil.");
            if (Before == null) return Result.Fail("Önce bir \"önce\" görüntüsü al.");
            After = Snapshots.Capture(Cell, title);
            Rows = Snapshots.Compare(Before, After, onlyChanged);
            Version++;
            string text = Snapshots.ToText(Before, After, Rows);
            LabReport.Remember("Önce / sonra karşılaştırması", text);
            return Result.Success($"Karşılaştırıldı: {Rows.Count} satır {(onlyChanged ? "değişti" : "listelendi")}.");
        }

        public static void Refilter(bool onlyChanged)
        {
            if (Before == null || After == null) return;
            Rows = Snapshots.Compare(Before, After, onlyChanged);
            Version++;
        }

        public static void Clear()
        {
            Before = After = null;
            Rows = new List<DiffRow>();
            Cell = null;
            Version++;
        }
    }

    // Rapor metinleri, Console / pano komutları ve oturum raporunun dışa aktarımı. Yalnız okur ve yazıya çevirir; oyuna dokunmaz.
    // Dosyalar kullanıcının seçtiği klasöre yazılır (varsayılan: proje kökünde Logs/MechanicsTestLab). Gerçek oyuncu kaydının
    // klasörüne ve Assets'e yazım reddedilir.
    public static class LabReport
    {
        public static string LastTitle { get; private set; } = "";
        public static string LastText { get; private set; } = "";
        public static string LastExportFolder { get; private set; } = "";

        // Son sayısal işlem (makbuz, karşılaştırma, iz, şans örneği, kontrol): "Son işlemi Console'a yaz" bunu yazar.
        public static void Remember(string title, string text)
        {
            LastTitle = title ?? "";
            LastText = text ?? "";
        }

        public static void Forget() { LastTitle = LastText = ""; }

        private static string CellText(GridPosition cell) => $"({cell.x}, {cell.z})";

        // ---------------------------------------------------------------- başlık
        public static string Header(bool truncated)
        {
            var text = new StringBuilder();
            Scenario scenario = TestSession.Current;
            RoundManager rounds = RoundManager.Instance;
            text.Append("MECHANICS TEST LAB RAPORU · ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
            text.Append("Senaryo: ").Append(scenario != null ? scenario.name : "?").Append('\n');
            text.Append("Profil: ").Append(TestSession.SourceProfile != null ? TestSession.SourceProfile.name : "?").Append(" (çalışma zamanı kopyası) · seed ")
                .Append(TestSession.Profile != null ? TestSession.Profile.bossSeed.ToString() : "?").Append('\n');
            text.Append("Round: ").Append(rounds != null ? rounds.CurrentRound.ToString() : "?").Append(" · oyun zamanı ").Append(Time.timeAsDouble.ToString("0.00")).Append(" sn · kare ").Append(Time.frameCount).Append('\n');
            BossRewardManager boss = BossRewardManager.Instance;
            text.Append("Aktif ödüller: ");
            if (boss == null || boss.Taken.Count == 0) text.Append("yok");
            else
            {
                var seen = new List<BossRewardSO>();
                foreach (BossRewardSO reward in boss.Taken)
                {
                    if (reward == null || seen.Contains(reward)) continue;
                    text.Append(seen.Count > 0 ? ", " : "").Append(reward.displayName).Append(" ×").Append(boss.Stacks(reward));
                    seen.Add(reward);
                }
            }
            text.Append('\n').Append("Test katkıları: ");
            if (TestStats.Contributions.Count == 0) text.Append("yok");
            for (int i = 0; i < TestStats.Contributions.Count; i++) text.Append(i > 0 ? "; " : "").Append(TestStats.Describe(TestStats.Contributions[i]));
            text.Append('\n').Append("Başlangıç koşulları: ");
            if (scenario != null)
                text.Append($"grid {scenario.gridSize} × {scenario.gridSize} · round {scenario.round} · level {(scenario.level > 0 ? scenario.level.ToString() : "1")} · " +
                            $"bitki {scenario.plantFill}{(scenario.plantHealth > 0 ? " (can " + scenario.plantHealth + ")" : "")} · boss {(string.IsNullOrEmpty(scenario.boss) ? "normal akış" : scenario.boss)}");
            text.Append('\n').Append("Şu anki kontrol: ")
                .Append($"otomatik saldırı {(TestAttack.AutoAttack ? "açık" : "kapalı")} · üretim {(TestAttack.ProductionPaused ? "duraklatıldı" : "açık")} · " +
                        $"round sayacı {(TestAttack.RoundTimerFrozen ? "donduruldu" : "akıyor")} · nişan {(TestAttack.Aim.HasValue ? "sabit" : "fare")} · olay kaydı seviyesi {LevelName(TestLog.Level)}")
                .Append('\n');
            text.Append("Kayıt kesildi mi: ").Append(truncated ? "EVET — sonuç eksik (ayrıntı aşağıda)" : "hayır").Append('\n');
            return text.ToString();
        }

        public static string LevelName(TestLog.LogLevel level) => level switch
        {
            TestLog.LogLevel.Off => "Kapalı",
            TestLog.LogLevel.Summary => "İşlem özeti",
            _ => "Ayrıntılı sayısal iz",
        };

        // Rapordaki herhangi bir kayıt eksik mi (kesilen iz, taşan olay kaydı).
        public static bool AnyTruncated()
        {
            foreach (TraceRecord record in TestTrace.Records) if (record.Truncated) return true;
            return TestLog.Dropped > 0;
        }

        // ---------------------------------------------------------------- saldırı izi
        public static string TraceTitle(TraceRecord record) =>
            $"#{record.Id} · S{record.AttackSerial} · {record.Hits.Count} vuruş · {record.Harvests.Count} hasat" + (record.Truncated ? " · KESİLDİ" : "");

        public static string TraceText(TraceRecord record)
        {
            var text = new StringBuilder();
            GridPosition center = FieldCells.Grid != null ? FieldCells.Grid.GetGridPosition(record.Center) : default;
            text.Append($"SALDIRI İZİ #{record.Id} · saldırı kimliği S{record.AttackSerial} · zincir kökü {(record.ChainRoot != 0 ? record.ChainRoot.ToString() : "yok (Zincir Hasat alınmamış)")}\n");
            text.Append($"Merkez {CellText(center)} · gerçek temas yarıçapı {record.Radius:0.###} · değen hücre {record.Cells} · canlı hedef {record.LivingTargets} · başlangıç karesi {record.StartFrame}\n");
            text.Append(record.Truncated
                ? $"DURUM: KAYIT KESİLDİ, SONUÇ EKSİK — {record.TruncatedReason}\n"
                : $"DURUM: TAMAMLANDI — bekleyen iş kalmadı ({record.EndTime - record.StartTime:0.000} sn oyun zamanı)\n");
            foreach (string warning in record.Interference) text.Append("UYARI: ").Append(warning).Append('\n');

            text.Append("\nVURUŞLAR (hesaplanan darbe hasarı ≠ gerçek can azalması ≠ taşan hasar)\n");
            if (record.Hits.Count == 0) text.Append("  vuruş yok (boş savuruş)\n");
            foreach (TraceHit hit in record.Hits)
            {
                text.Append($"  #{hit.Index} +{hit.Time:0.000}s k{hit.Frame} [{hit.Kind}] {HitType(hit)} · kaynak {hit.Source} → hedef {CellText(hit.Target)} {hit.Plant} [{hit.Rarity}]\n");
                text.Append($"       can {hit.HealthBefore} → {hit.HealthAfter} / {hit.MaxHealth} · hesaplanan darbe {hit.Computed} · gerçek can azalması {hit.Decrease} · taşan {hit.Overkill}" +
                            (hit.Killed ? " · HASAT" : "") + (hit.Crit ? " · KRİTİK" : "") + $" · bu saldırıya bağ: {(hit.Linked ? "kesin" : "kanıt yok")}\n");
                foreach (string line in hit.Calculation) text.Append("       ").Append(line).Append('\n');
            }

            text.Append("\nHASATLAR VE ÖDÜLLER\n");
            if (record.Harvests.Count == 0) text.Append("  hasat yok\n");
            foreach (TraceHarvest harvest in record.Harvests)
            {
                text.Append($"  #{harvest.Index} +{harvest.Time:0.000}s {CellText(harvest.Cell)} {harvest.Plant} [{harvest.Rarity}] · öldüren: {harvest.KilledBy} · bağ: {(harvest.Linked ? "kesin" : "kanıt yok")}\n");
                if (harvest.HasReward)
                {
                    text.Append($"       {harvest.Resource}: taban ödül {harvest.BaseReward} × saksı kaynak çarpanı {harvest.ResourceMultiplier:0.####} → yuvarlanan {harvest.RewardRounded}" +
                                $" → çift ödül (şans {harvest.DuplicateChance * 100f:0.##}%): {(harvest.Duplicated ? "EVET" : "hayır")} → {harvest.RewardAfterDuplicate}" +
                                $" → uzmanlaşma / başlangıç ölçeği → {harvest.RewardAfterSpecialization} → Gezgin Bereket: {(harvest.Bounty ? "EVET" : "hayır")} → bankaya eklenen {harvest.RewardFinal}\n");
                    text.Append($"       XP: taban {harvest.BaseXp} × saksı XP çarpanı {harvest.XpMultiplier:0.####} × nadirlik XP çarpanı {harvest.RarityXpMultiplier:0.####} → yuvarlanan {harvest.XpRounded:0.##}" +
                                $" → eklenen {harvest.XpFinal:0.##}\n");
                }
                else text.Append("       kaynak / XP hesabı kayıtta yok (bu hasat ödül vermedi ya da kayıt kesildi)\n");
                text.Append(harvest.HasScore
                    ? $"       Harvest Score: nadirlik {harvest.Rarity} · oyuncu çarpanı {harvest.ScorePlayerMultiplier:0.####} · saksı çarpanı {harvest.ScorePlanterMultiplier:0.####} → {harvest.ScoreAward} → başlangıç ölçeğinden sonra eklenen {harvest.ScoreFinal}\n"
                    : "       Harvest Score hesabı kayıtta yok (skor verilmedi)\n");
            }

            text.Append("\nDEVAM OLAYLARI\n");
            if (record.Notes.Count == 0) text.Append("  yok\n");
            foreach (string note in record.Notes) text.Append("  ").Append(note).Append('\n');

            text.Append("\nHESAP FARKI (kayıt başı → kayıt sonu; gerçek bakiyeler)\n");
            text.Append(EconomyText(record));
            return text.ToString();
        }

        public static string TypeName(DamageType type) => type switch
        {
            DamageType.Direct => "Doğrudan",
            DamageType.Explosion => "Patlama",
            DamageType.Electric => "Elektrik",
            DamageType.Tornado => "Kasırga",
            DamageType.Boomerang => "Bumerang",
            DamageType.Reward => "Ödül etkisi",
            _ => type.ToString(),
        };

        private static string HitType(TraceHit hit) =>
            hit.Type == DamageType.Reward ? "ödül etkisi " + hit.Reward : TypeName(hit.Type) + (hit.Generation > 0 ? $" (zincir nesli {hit.Generation})" : "") + (hit.Echo ? " (zincir dışı / ikinci darbe)" : "");

        // Kayıttaki hasat ödüllerinin toplamı ile gerçek bakiye farkının karşılaştırması.
        public sealed class EconomySums
        {
            public double Xp;
            public long Score;
            public int Gold, Iron, Stone, Harvests, WithoutReward, WithoutScore;
        }

        public static EconomySums Sum(TraceRecord record)
        {
            var sums = new EconomySums { Harvests = record.Harvests.Count };
            foreach (TraceHarvest harvest in record.Harvests)
            {
                if (harvest.HasReward)
                {
                    sums.Xp += harvest.XpFinal;
                    if (harvest.Resource == ResourceType.Gold) sums.Gold += harvest.RewardFinal;
                    else if (harvest.Resource == ResourceType.Iron) sums.Iron += harvest.RewardFinal;
                    else sums.Stone += harvest.RewardFinal;
                }
                else sums.WithoutReward++;
                if (harvest.HasScore) sums.Score += harvest.ScoreFinal;
                else sums.WithoutScore++;
            }
            return sums;
        }

        private static string Match(double recorded, double real) =>
            Math.Abs(recorded - real) < 0.5 ? "EŞLEŞİYOR" : $"FARKLI ({real - recorded:+0.##;-0.##}: kayıt dışı gelir / harcama karışmış ya da kayıt eksik)";

        public static string EconomyText(TraceRecord record)
        {
            Economy a = record.Before, b = record.After;
            EconomySums sums = Sum(record);
            var text = new StringBuilder();
            text.Append($"  Hasat sayısı (kayıtta): {sums.Harvests}\n");
            text.Append($"  Kazanılan toplam XP: {a.XpEarned:0.##} → {b.XpEarned:0.##} (fark {b.XpEarned - a.XpEarned:+0.##;-0.##;0}) · kayıtlı hasat XP toplamı {sums.Xp:0.##} → {Match(sums.Xp, b.XpEarned - a.XpEarned)}\n");
            text.Append($"  Saklı XP (level'a harcanmamış, kalan): {a.XpStored:0.##} → {b.XpStored:0.##} — kazanılan XP'den FARKLIDIR: level alındıysa maliyeti düşülmüştür\n");
            text.Append($"  Level: {a.Level} → {b.Level} ({b.Level - a.Level:+0;-0;0}) · bekleyen kart seçimi {a.PendingChoices} → {b.PendingChoices} · bu run'da verilen toplam seçim hakkı {a.GrantedChoices} → {b.GrantedChoices} ({b.GrantedChoices - a.GrantedChoices:+0;-0;0})\n");
            text.Append($"  Altın: {a.Gold} → {b.Gold} (fark {b.Gold - a.Gold:+0;-0;0}) · kayıtlı {sums.Gold} → {Match(sums.Gold, b.Gold - a.Gold)}\n");
            text.Append($"  Demir: {a.Iron} → {b.Iron} (fark {b.Iron - a.Iron:+0;-0;0}) · kayıtlı {sums.Iron} → {Match(sums.Iron, b.Iron - a.Iron)}\n");
            text.Append($"  Taş: {a.Stone} → {b.Stone} (fark {b.Stone - a.Stone:+0;-0;0}) · kayıtlı {sums.Stone} → {Match(sums.Stone, b.Stone - a.Stone)}\n");
            text.Append($"  Harvest Score: {a.Score} → {b.Score} (fark {b.Score - a.Score:+0;-0;0}) · kayıtlı {sums.Score} → {Match(sums.Score, b.Score - a.Score)}\n");
            if (sums.WithoutReward > 0) text.Append($"  NOT: {sums.WithoutReward} hasadın kaynak / XP hesabı kayıtta yok.\n");
            if (record.Interference.Count > 0) text.Append("  NOT: karışma uyarısı var; farklar yalnız bu saldırıya ait olmayabilir.\n");
            return text.ToString();
        }

        public static string TraceHitsCsv(IEnumerable<TraceRecord> records)
        {
            var text = new StringBuilder("iz;saldiri;vurus;zaman_sn;kare;tur;hasar_turu;odul_kaynagi;nesil;kok;ikinci_darbe;kaynak;hedef_x;hedef_z;bitki;nadirlik;" +
                                         "can_once;can_sonra;azami_can;hesaplanan_darbe;gercek_can_azalmasi;tasan;hasat;kritik;bag_kesin;taban;sapma;ritim_carpani;odul_saldiri_carpani;" +
                                         "kritik_sansi;kritik_carpani;yuvarlanan;kritikten_sonra;saksi_carpani\n");
            foreach (TraceRecord record in records)
                foreach (TraceHit hit in record.Hits)
                {
                    text.Append(record.Id).Append(';').Append(record.AttackSerial).Append(';').Append(hit.Index).Append(';').Append(Num(hit.Time)).Append(';').Append(hit.Frame).Append(';')
                        .Append(Snapshots.Csv(hit.Kind)).Append(';').Append(hit.Type).Append(';').Append(hit.Type == DamageType.Reward ? hit.Reward.ToString() : "").Append(';')
                        .Append(hit.Generation).Append(';').Append(hit.Root).Append(';').Append(hit.Echo ? 1 : 0).Append(';').Append(Snapshots.Csv(hit.Source)).Append(';')
                        .Append(hit.Target.x).Append(';').Append(hit.Target.z).Append(';').Append(Snapshots.Csv(hit.Plant)).Append(';').Append(hit.Rarity).Append(';')
                        .Append(hit.HealthBefore).Append(';').Append(hit.HealthAfter).Append(';').Append(hit.MaxHealth).Append(';').Append(hit.Computed).Append(';').Append(hit.Decrease).Append(';')
                        .Append(hit.Overkill).Append(';').Append(hit.Killed ? 1 : 0).Append(';').Append(hit.Crit ? 1 : 0).Append(';').Append(hit.Linked ? 1 : 0).Append(';');
                    if (hit.HasDirect)
                        text.Append(Num(hit.BaseDamage)).Append(';').Append(Num(hit.Variance)).Append(';').Append(Num(hit.RhythmMultiplier)).Append(';').Append(Num(hit.FeedMultiplier)).Append(';')
                            .Append(Num(hit.CritChance)).Append(';').Append(Num(hit.CritMultiplier)).Append(';').Append(hit.DamageBeforeCrit).Append(';').Append(hit.DamageAfterCrit).Append(';')
                            .Append(Num(hit.PlanterMultiplier));
                    else text.Append(";;;;;;;;");
                    text.Append('\n');
                }
            return text.ToString();
        }

        public static string TraceHarvestsCsv(IEnumerable<TraceRecord> records)
        {
            var text = new StringBuilder("iz;hasat;zaman_sn;x;z;bitki;nadirlik;olduren;kaynak_turu;taban_odul;kaynak_carpani;yuvarlanan;cift_odul_sansi;cift_odul;cift_sonrasi;" +
                                         "olcek_sonrasi;bereket;eklenen_kaynak;taban_xp;xp_carpani;nadirlik_xp_carpani;yuvarlanan_xp;eklenen_xp;skor_oyuncu_carpani;skor_saksi_carpani;skor;eklenen_skor\n");
            foreach (TraceRecord record in records)
                foreach (TraceHarvest h in record.Harvests)
                    text.Append(record.Id).Append(';').Append(h.Index).Append(';').Append(Num(h.Time)).Append(';').Append(h.Cell.x).Append(';').Append(h.Cell.z).Append(';')
                        .Append(Snapshots.Csv(h.Plant)).Append(';').Append(h.Rarity).Append(';').Append(Snapshots.Csv(h.KilledBy)).Append(';').Append(h.Resource).Append(';')
                        .Append(h.BaseReward).Append(';').Append(Num(h.ResourceMultiplier)).Append(';').Append(h.RewardRounded).Append(';').Append(Num(h.DuplicateChance)).Append(';')
                        .Append(h.Duplicated ? 1 : 0).Append(';').Append(h.RewardAfterDuplicate).Append(';').Append(h.RewardAfterSpecialization).Append(';').Append(h.Bounty ? 1 : 0).Append(';')
                        .Append(h.RewardFinal).Append(';').Append(h.BaseXp).Append(';').Append(Num(h.XpMultiplier)).Append(';').Append(Num(h.RarityXpMultiplier)).Append(';')
                        .Append(Num(h.XpRounded)).Append(';').Append(Num(h.XpFinal)).Append(';').Append(Num(h.ScorePlayerMultiplier)).Append(';').Append(Num(h.ScorePlanterMultiplier)).Append(';')
                        .Append(h.ScoreAward).Append(';').Append(h.ScoreFinal).Append('\n');
            return text.ToString();
        }

        // CSV sayıları: ondalık ayırıcı nokta (kültürden bağımsız), alan ayırıcı noktalı virgül.
        private static string Num(double value) => value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

        // ---------------------------------------------------------------- Console ve pano
        // Açık kullanıcı komutudur; kayıt kendiliğinden Console'a yazmaz.
        public static Result ToConsole(string title, string text)
        {
            if (string.IsNullOrEmpty(text)) return Result.Fail("Yazılacak kayıt yok.");
            const int chunk = 12000;
            int parts = (text.Length + chunk - 1) / chunk;
            for (int i = 0; i < parts; i++)
                Debug.Log($"[Mechanics Test Lab] {title}{(parts > 1 ? $" ({i + 1}/{parts})" : "")}\n{text.Substring(i * chunk, Math.Min(chunk, text.Length - i * chunk))}");
            return Result.Success($"Console'a yazıldı: {title}" + (parts > 1 ? $" ({parts} parça)" : ""));
        }

        public static Result LastToConsole() => LastText.Length > 0 ? ToConsole(LastTitle, LastText) : Result.Fail("Henüz sayısal bir işlem yapılmadı.");

        public static Result Copy(string what, string text)
        {
            if (string.IsNullOrEmpty(text)) return Result.Fail("Kopyalanacak kayıt yok.");
            EditorGUIUtility.systemCopyBuffer = text;
            return Result.Success($"Panoya kopyalandı: {what} ({text.Length} karakter).");
        }

        // ---------------------------------------------------------------- dışa aktarım
        public static string DefaultFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MechanicsTestLab"));

        [Serializable] private sealed class JsonRow { public string label, scope, unit, before, after, delta, ratio, source, note; }
        [Serializable] private sealed class JsonReceipt { public string id, name, verdict, refusal, referencePlanter; public bool success; public int stacksBefore, stacksAfter, maxStacks, choicesBefore, choicesAfter, round; public List<string> gains, costs, mechanics, newlyExcluded; public List<JsonRow> changes; }
        [Serializable] private sealed class JsonHit { public int index, frame, generation, root, healthBefore, healthAfter, maxHealth, computed, decrease, overkill, targetX, targetZ; public double time; public string kind, type, rewardSource, source, plant, rarity; public bool echo, linked, crit, killed, foreign; public List<string> calculation; }
        [Serializable] private sealed class JsonHarvest { public int index, frame, x, z, baseReward, rewardFinal, baseXp, scoreFinal; public double time, xpFinal; public string plant, rarity, killedBy, resource; public bool linked, duplicated, bounty, hasReward, hasScore; }
        [Serializable] private sealed class JsonEconomy { public double xpEarned, xpStored; public int level, pendingChoices, grantedChoices, gold, iron, stone; public long score; }
        [Serializable] private sealed class JsonTrace { public int id, attackSerial, chainRoot, cells, livingTargets; public float radius; public bool complete, truncated; public string truncatedReason; public JsonEconomy before, after; public List<JsonHit> hits; public List<JsonHarvest> harvests; public List<string> notes, interference; }
        [Serializable] private sealed class JsonCheckLine { public string name, expected, actual, tolerance, status; }
        [Serializable] private sealed class JsonCheck { public string id, group, title, precondition, action, status, note, coverage; public List<JsonCheckLine> lines; }
        [Serializable] private sealed class JsonChance { public string name, behavior, note; public int seed, trials, opportunities, rolled, started, rejected; public double observed, expected, low, high; public bool complete; }
        [Serializable]
        private sealed class JsonReport
        {
            public string tool = "Mechanics Test Lab", created, scenario, profile, logLevel, header;
            public int seed, round;
            public bool truncated;
            public List<string> activeRewards = new(), testContributions = new(), buffs = new(), log = new();
            public List<JsonRow> comparison = new();
            public List<JsonReceipt> receipts = new();
            public List<JsonTrace> traces = new();
            public List<JsonCheck> checks = new();
            public List<JsonChance> chance = new();
        }

        private static JsonRow Row(DiffRow row) => new JsonRow { label = row.Label, scope = row.Scope, unit = row.Unit, before = row.Before, after = row.After, delta = row.Delta, ratio = row.Ratio, source = row.Source, note = row.Note };
        private static JsonEconomy Json(Economy e) => new JsonEconomy { xpEarned = e.XpEarned, xpStored = e.XpStored, level = e.Level, pendingChoices = e.PendingChoices, grantedChoices = e.GrantedChoices, gold = e.Gold, iron = e.Iron, stone = e.Stone, score = e.Score };

        private static JsonReport Build()
        {
            bool truncated = AnyTruncated();
            RoundManager rounds = RoundManager.Instance;
            var report = new JsonReport
            {
                created = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), scenario = TestSession.Current != null ? TestSession.Current.name : "",
                profile = TestSession.SourceProfile != null ? TestSession.SourceProfile.name : "", seed = TestSession.Profile != null ? TestSession.Profile.bossSeed : 0,
                round = rounds != null ? rounds.CurrentRound : 0, truncated = truncated, logLevel = LevelName(TestLog.Level), header = Header(truncated),
            };
            BossRewardManager boss = BossRewardManager.Instance;
            if (boss != null)
                foreach (BossRewardSO reward in boss.Taken)
                {
                    string line = reward != null ? $"{reward.id} ×{boss.Stacks(reward)}" : "";
                    if (line.Length > 0 && !report.activeRewards.Contains(line)) report.activeRewards.Add(line);
                }
            foreach (StatModifier modifier in TestStats.Contributions) report.testContributions.Add(TestStats.Describe(modifier));
            var buffs = new StringBuilder();
            if (TestSession.Ready) BuffWatch.Describe(buffs);
            foreach (string line in buffs.ToString().Split('\n')) if (line.Length > 0) report.buffs.Add(line);
            foreach (DiffRow row in Comparison.Rows) report.comparison.Add(Row(row));
            foreach (RewardReceipt receipt in TestRewards.Receipts)
            {
                var json = new JsonReceipt
                {
                    id = receipt.Id, name = receipt.Name, verdict = receipt.Verdict, refusal = receipt.Refusal, referencePlanter = receipt.ReferencePlanter, success = receipt.Success,
                    stacksBefore = receipt.StacksBefore, stacksAfter = receipt.StacksAfter, maxStacks = receipt.MaxStacks, choicesBefore = receipt.ChoicesBefore, choicesAfter = receipt.ChoicesAfter,
                    round = receipt.Round, gains = new List<string>(receipt.Gains), costs = new List<string>(receipt.Costs), mechanics = new List<string>(receipt.Mechanics),
                    newlyExcluded = new List<string>(receipt.NewlyExcluded), changes = new List<JsonRow>(),
                };
                foreach (DiffRow row in receipt.Changes) json.changes.Add(Row(row));
                report.receipts.Add(json);
            }
            foreach (TraceRecord record in TestTrace.Records)
            {
                var json = new JsonTrace
                {
                    id = record.Id, attackSerial = record.AttackSerial, chainRoot = record.ChainRoot, cells = record.Cells, livingTargets = record.LivingTargets, radius = record.Radius,
                    complete = record.Complete, truncated = record.Truncated, truncatedReason = record.TruncatedReason, before = Json(record.Before), after = Json(record.After),
                    hits = new List<JsonHit>(), harvests = new List<JsonHarvest>(), notes = new List<string>(record.Notes), interference = new List<string>(record.Interference),
                };
                foreach (TraceHit hit in record.Hits)
                    json.hits.Add(new JsonHit
                    {
                        index = hit.Index, frame = hit.Frame, time = hit.Time, kind = hit.Kind, type = hit.Type.ToString(), rewardSource = hit.Type == DamageType.Reward ? hit.Reward.ToString() : "",
                        generation = hit.Generation, root = hit.Root, echo = hit.Echo, linked = hit.Linked, crit = hit.Crit, killed = hit.Killed, foreign = hit.Foreign, source = hit.Source,
                        plant = hit.Plant, rarity = hit.Rarity.ToString(), targetX = hit.Target.x, targetZ = hit.Target.z, healthBefore = hit.HealthBefore, healthAfter = hit.HealthAfter,
                        maxHealth = hit.MaxHealth, computed = hit.Computed, decrease = hit.Decrease, overkill = hit.Overkill, calculation = new List<string>(hit.Calculation),
                    });
                foreach (TraceHarvest h in record.Harvests)
                    json.harvests.Add(new JsonHarvest
                    {
                        index = h.Index, frame = h.Frame, time = h.Time, x = h.Cell.x, z = h.Cell.z, plant = h.Plant, rarity = h.Rarity.ToString(), killedBy = h.KilledBy, linked = h.Linked,
                        resource = h.Resource.ToString(), baseReward = h.BaseReward, rewardFinal = h.RewardFinal, baseXp = h.BaseXp, xpFinal = h.XpFinal, scoreFinal = h.ScoreFinal,
                        duplicated = h.Duplicated, bounty = h.Bounty, hasReward = h.HasReward, hasScore = h.HasScore,
                    });
                report.traces.Add(json);
            }
            foreach (CheckResult result in NumericChecks.Results)
            {
                var json = new JsonCheck
                {
                    id = result.Id, group = result.Group, title = result.Title, precondition = result.Precondition, action = result.Action, status = NumericChecks.StatusText(result.Status),
                    note = result.Note, coverage = NumericChecks.CoverageText(result.Coverage), lines = new List<JsonCheckLine>(),
                };
                foreach (CheckLine line in result.Lines)
                    json.lines.Add(new JsonCheckLine { name = line.Name, expected = line.Expected, actual = line.Actual, tolerance = line.Tolerance, status = NumericChecks.StatusText(line.Status) });
                report.checks.Add(json);
            }
            foreach (ChanceSample sample in ChanceSampler.Samples)
                report.chance.Add(new JsonChance
                {
                    name = sample.Name, behavior = sample.Behavior.ToString(), note = sample.Note, seed = sample.Seed, trials = sample.Trials, opportunities = sample.Opportunities,
                    rolled = sample.Rolled, started = sample.Started, rejected = sample.Rejected, observed = sample.Observed, expected = sample.Expected, low = sample.Low, high = sample.High,
                    complete = sample.Complete,
                });
            var entries = new List<TestLog.Entry>();
            TestLog.Collect(TestLog.Kind.All, null, 0, entries);
            foreach (TestLog.Entry entry in entries) report.log.Add(TestLog.Format(entry));
            return report;
        }

        public static string SessionText()
        {
            bool truncated = AnyTruncated();
            var text = new StringBuilder(Header(truncated));
            if (TestLog.Dropped > 0) text.Append($"Olay kaydı halkası doldu: en eski {TestLog.Dropped} satır düştü (kapasite {TestLog.Capacity}).\n");
            text.Append("\n=== AKTİF GEÇİCİ / KOŞULLU ETKİLER ===\n");
            var buffs = new StringBuilder();
            if (TestSession.Ready) BuffWatch.Describe(buffs);
            text.Append(buffs.Length > 0 ? buffs.ToString() : "oturum hazır değil\n");
            text.Append("\n=== ÖNCE / SONRA ===\n");
            text.Append(Comparison.Before != null && Comparison.After != null ? Snapshots.ToText(Comparison.Before, Comparison.After, Comparison.Rows) : "karşılaştırma alınmadı\n");
            text.Append("\n=== ÖDÜL MAKBUZLARI ===\n");
            if (TestRewards.Receipts.Count == 0) text.Append("makbuz yok\n");
            foreach (RewardReceipt receipt in TestRewards.Receipts) text.Append(receipt.ToText()).Append('\n');
            text.Append("\n=== SALDIRI İZLERİ ===\n");
            if (TestTrace.Records.Count == 0) text.Append("iz yok\n");
            foreach (TraceRecord record in TestTrace.Records) text.Append(TraceText(record)).Append('\n');
            text.Append("\n=== BEKLENEN / GERÇEKLEŞEN KONTROLLER ===\n");
            text.Append(NumericChecks.Results.Count > 0 ? NumericChecks.Summary() + "\n" : "kontrol çalıştırılmadı\n");
            foreach (CheckResult result in NumericChecks.Results) text.Append(NumericChecks.ToText(result)).Append('\n');
            text.Append("\n=== ŞANS İSTATİSTİKLERİ ===\n");
            if (ChanceSampler.Samples.Count == 0) text.Append("örnek yok\n");
            foreach (ChanceSample sample in ChanceSampler.Samples) text.Append(sample.ToText()).Append('\n');
            text.Append("\n=== OLAY KAYDI ===\n");
            var entries = new List<TestLog.Entry>();
            TestLog.Collect(TestLog.Kind.All, null, 0, entries);
            if (entries.Count == 0) text.Append("kayıt yok (seviye: " + LevelName(TestLog.Level) + ")\n");
            foreach (TestLog.Entry entry in entries) text.Append(TestLog.Format(entry)).Append('\n');
            return text.ToString();
        }

        // Hedef klasör güvenli mi: gerçek oyuncu kaydının klasörü ve Assets reddedilir.
        public static bool SafeFolder(string folder, out string reason)
        {
            reason = "";
            if (string.IsNullOrWhiteSpace(folder)) { reason = "klasör seçilmedi"; return false; }
            string full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string save = Path.GetFullPath(Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Inside(full, save)) { reason = "gerçek oyuncu kaydının klasörü; rapor oraya yazılmaz"; return false; }
            if (Inside(full, assets)) { reason = "Assets klasörü; rapor proje asset'i değildir"; return false; }
            return true;
        }

        private static bool Inside(string path, string root) =>
            path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        // Oturum raporu: okunabilir metin + JSON + sayısal tabloların CSV'leri, klasörde zaman damgalı bir alt klasöre.
        public static Result Export(string folder)
        {
            if (!SafeFolder(folder, out string reason)) return Result.Fail("Rapor yazılmadı: " + reason + ".");
            try
            {
                string target = Path.Combine(Path.GetFullPath(folder), "rapor-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(target);
                var utf8 = new UTF8Encoding(true);
                File.WriteAllText(Path.Combine(target, "rapor.txt"), SessionText(), utf8);
                File.WriteAllText(Path.Combine(target, "rapor.json"), JsonUtility.ToJson(Build(), true), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(target, "once-sonra.csv"), Snapshots.ToCsv(Comparison.Rows), utf8);
                File.WriteAllText(Path.Combine(target, "saldiri-izi-vuruslar.csv"), TraceHitsCsv(TestTrace.Records), utf8);
                File.WriteAllText(Path.Combine(target, "saldiri-izi-hasatlar.csv"), TraceHarvestsCsv(TestTrace.Records), utf8);
                File.WriteAllText(Path.Combine(target, "kontroller.csv"), NumericChecks.ToCsv(), utf8);
                File.WriteAllText(Path.Combine(target, "sans.csv"), ChanceSampler.ToCsv(), utf8);
                LastExportFolder = target;
                return Result.Success("Oturum raporu yazıldı (rapor.txt, rapor.json ve 5 CSV): " + target);
            }
            catch (Exception e) { return Result.Fail("Rapor yazılamadı: " + e.Message); }
        }
    }
}
