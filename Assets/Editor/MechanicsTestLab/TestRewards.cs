using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MechanicsTestLab
{
    // Boss ödülü komutları. İki ayrı yol vardır ve panel ikisini ayrı gösterir:
    //  A) NORMAL KURAL TESTİ — boss round'u oyunun round sonu akışıyla biter; teklif oyunun teklif üreticisinden (aşama, uygunluk,
    //     stack, dışlama, seed) çıkar ve ödül oyunun kendi ekranından, Choose yoluyla alınır.
    //  B) KONTROLLÜ KURULUM — teklif zarı ve round beklenmeden seçilen ödül kurulur; alınabilirlik denetimi (BlockOf: stack,
    //     dışlama, ön koşul, seçim hakkı aralığı, koşul) ve uygulama (TryApply: kazanç, bedel, stat, etki) oyununkidir. Hiçbir
    //     kural sessizce aşılmaz; reddin nedeni yazılır.
    // Ödül geri alma oyunda yoktur ve buraya da eklenmedi: çakışan ödülü kaldırmak, ödül listesi değiştirilmiş senaryonun temiz
    // oturumda yeniden kurulmasıdır (TestSession.Rebuild).
    public static class TestRewards
    {
        // Kontrollü kurulumda kullanılan yerleşim konumları (senaryo yakalama için; canlı nesne tutulmaz).
        private static readonly Dictionary<string, int> placements = new();
        private static readonly List<BossRewardSO> buffer = new();
        private static readonly List<int> slots = new();

        public static BossRewardManager Boss => BossRewardManager.Instance;

        public static void Forget() { placements.Clear(); receipts.Clear(); }
        public static int PlacementOf(string id) => placements.TryGetValue(id, out int placement) ? placement : RunEffectSO.NoPlacement;

        public static void Catalog(List<BossRewardSO> into)
        {
            LabCatalog.Rewards(Boss != null ? Boss.Pool : null, into);
        }

        public static string BlockText(BossRewardSO reward, BossRewardBlock block)
        {
            switch (block)
            {
                case BossRewardBlock.None: return "alınabilir";
                case BossRewardBlock.StackLimit: return $"stack sınırı dolu ({reward.maxStacks})";
                case BossRewardBlock.Exclusive: return "dışlayan ödül alınmış: " + ConflictNames(reward);
                case BossRewardBlock.Prerequisite: return "ön koşul yok: önce " + (reward.requires != null ? reward.requires.displayName : "?");
                case BossRewardBlock.LevelChoiceRange:
                    return $"level başına seçim hakkı {RunPower.MinLevelChoices}–{RunPower.MaxLevelChoices} aralığının dışına çıkardı";
                case BossRewardBlock.BehaviorLimitRange: return "kasırga / bumerang sınırı teknik tavanı aşardı";
                case BossRewardBlock.Condition: return "koşul tutmuyor: " + ConditionText(reward);
                default: return block.ToString();
            }
        }

        public static string ConditionText(BossRewardSO reward)
        {
            if (reward.effect != null && RunEffects.Instance != null && !RunEffects.Instance.CanTake(reward.effect))
                return "ödülün etkisi şu an kurulamıyor (gereken etki / kritik şansı / konum yok)";
            return reward.condition switch
            {
                BossRewardCondition.AnyPlanterHasModifiedStat => "bu ödülün dokunduğu stat'a sahip yerleşmiş saksı yok",
                BossRewardCondition.AnyBehaviorPlanter => "davranışı (patlama, kasırga, bumerang, elektrik) olan yerleşmiş saksı yok",
                BossRewardCondition.FieldRunsLow => $"son round'da tarla doluluğu eşiğin ({reward.conditionThreshold:0.##}) altında değil",
                BossRewardCondition.AnyPlanterHasConditionStat => $"{reward.conditionStat} değeri 0'dan büyük yerleşmiş saksı yok",
                _ => "ödül şu an hiçbir şey değiştirmiyor",
            };
        }

        // Bu ödülü dışlayan, bu run'da ALINMIŞ ödüller.
        public static void Conflicts(BossRewardSO reward, List<BossRewardSO> into)
        {
            into.Clear();
            if (Boss == null || reward == null || string.IsNullOrEmpty(reward.exclusiveGroup)) return;
            foreach (BossRewardSO other in Boss.Taken)
                if (other != reward && other.exclusiveGroup == reward.exclusiveGroup) into.Add(other);
        }

        private static string ConflictNames(BossRewardSO reward)
        {
            Conflicts(reward, buffer);
            var text = new StringBuilder();
            foreach (BossRewardSO other in buffer) text.Append(text.Length > 0 ? ", " : "").Append(other.displayName);
            return text.Length > 0 ? text.ToString() : "?";
        }

        // Ödülün kimliği: aşama, stack, koşul, bedel, dışlama grubu ve kart metni (hepsi oyun verisinden).
        public static string Describe(BossRewardSO reward)
        {
            var text = new StringBuilder();
            BossRewardManager boss = Boss;
            BossRewardStage stage = boss != null ? boss.StageOf(reward) : null;
            text.Append(reward.displayName).Append("  [").Append(reward.id).Append("]\n");
            text.Append("Aşama: ").Append(stage != null ? $"{stage.displayName} (R{stage.firstRound}+; ilk teklif R{boss.FirstOfferRound(stage)})" : "aşamasız havuz").Append('\n');
            text.Append("Stack: ").Append(boss != null ? boss.Stacks(reward) : 0).Append(" / ").Append(reward.maxStacks).Append('\n');
            if (reward.requires != null) text.Append("Ön koşul: ").Append(reward.requires.displayName).Append('\n');
            if (reward.condition != BossRewardCondition.Always) text.Append("Koşul: ").Append(reward.condition).Append('\n');
            if (reward.HasCost)
            {
                text.Append("Bedel:");
                if (reward.levelChoiceDelta < 0) text.Append($" level başına seçim {reward.levelChoiceDelta}");
                if (BossRewardSO.IsCost(reward.directDamageMultiplier)) text.Append($" doğrudan vuruş ×{reward.directDamageMultiplier:0.##}");
                if (BossRewardSO.IsCost(reward.rareDirectMultiplier)) text.Append($" nadir doğrudan ×{reward.rareDirectMultiplier:0.##}");
                if (BossRewardSO.IsCost(reward.behaviorDamageMultiplier)) text.Append($" davranış hasarı ×{reward.behaviorDamageMultiplier:0.##}");
                text.Append('\n');
            }
            if (reward.levelChoiceDelta > 0) text.Append($"Kazanç: level başına seçim +{reward.levelChoiceDelta}\n");
            if (!string.IsNullOrEmpty(reward.exclusiveGroup))
            {
                text.Append("Dışlama grubu: ").Append(reward.exclusiveGroup);
                if (boss != null)
                {
                    boss.ExclusiveWith(reward, buffer);
                    foreach (BossRewardSO other in buffer) text.Append(" · ").Append(other.displayName);
                }
                text.Append('\n');
            }
            if (reward.effect != null) text.Append("Etki: ").Append(reward.effect.Summary).Append('\n');
            string note = boss != null ? boss.NoteFor(reward) : reward.note;
            if (!string.IsNullOrEmpty(note)) text.Append("Kart metni: ").Append(note).Append('\n');
            if (boss != null)
            {
                BossRewardBlock block = boss.BlockOf(reward, true);
                text.Append("Durum: ").Append(BlockText(reward, block));
            }
            return text.ToString();
        }

        public static void Placements(BossRewardSO reward, List<int> into)
        {
            into.Clear();
            if (reward != null && reward.effect != null && reward.effect.NeedsPlacement && RunEffects.Instance != null)
                reward.effect.CollectPlacements(RunEffects.Instance, into);
        }

        // Panelden kurulan her ödülün makbuzu (son 30): istenen, sonuç, gerçek değişim.
        public const int KeptReceipts = 30;
        private static readonly List<RewardReceipt> receipts = new();
        public static IReadOnlyList<RewardReceipt> Receipts => receipts;
        public static RewardReceipt LastReceipt => receipts.Count > 0 ? receipts[receipts.Count - 1] : null;

        // B) Kontrollü kurulum. Kurulumdan önce ve sonra gerçek durum okunur ve bir makbuz yazılır: listede ödül adının görünmesi
        // tek başına "mekanik uygulandı" kanıtı sayılmaz.
        public static Result Grant(BossRewardSO reward, int placement = RunEffectSO.NoPlacement)
        {
            if (reward == null || Boss == null) return GrantCore(reward, placement);
            RewardReceipt receipt = RewardReceipt.Begin(reward);
            Result result = GrantCore(reward, placement);
            receipt.Finish(result);
            receipts.Add(receipt);
            while (receipts.Count > KeptReceipts) receipts.RemoveAt(0);
            return result;
        }

        private static Result GrantCore(BossRewardSO reward, int placement)
        {
            BossRewardManager boss = Boss;
            if (boss == null) return Result.Fail("BossRewardManager yok.");
            if (reward == null) return Result.Fail("Ödül yok.");
            BossRewardBlock block = boss.BlockOf(reward, true);
            if (block != BossRewardBlock.None) return Result.Fail($"{reward.displayName} kurulmadı — {BlockText(reward, block)}.");
            if (boss.NeedsPlacement(reward))
            {
                Placements(reward, slots);
                if (slots.Count == 0) return Result.Fail($"{reward.displayName} kurulmadı — geçerli yerleşim konumu yok.");
                if (placement == RunEffectSO.NoPlacement) placement = slots[0];
                else if (!slots.Contains(placement))
                    return Result.Fail($"{reward.displayName} kurulmadı — seçilen konum ({AntennaSlots.Name(placement)}) geçerli değil; başka konuma taşınmadı.");
            }
            if (!boss.EditorGrant(reward, out string refusal, placement)) return Result.Fail($"{reward.displayName} kurulmadı — {refusal}.");
            if (placement != RunEffectSO.NoPlacement) placements[reward.id] = placement;
            TestLog.Note(TestLog.Kind.Reward, $"Ödül kuruldu (kontrollü): {reward.displayName} · stack {boss.Stacks(reward)}/{reward.maxStacks}" +
                                              (placement != RunEffectSO.NoPlacement ? $" · {AntennaSlots.Name(placement)}" : ""));
            return Result.Success($"{reward.displayName} kuruldu ({boss.Stacks(reward)}/{reward.maxStacks})" +
                                  (placement != RunEffectSO.NoPlacement ? $" · {AntennaSlots.Name(placement)}" : "") + ".");
        }

        // Ön koşul zinciriyle birlikte kurar (anten → hat → devre): eksik ön koşullar da aynı denetimden geçerek kurulur.
        public static Result GrantWithPrerequisites(BossRewardSO reward, List<Result> report = null)
        {
            if (reward == null) return Result.Fail("Ödül yok.");
            if (reward.requires != null && Boss != null && Boss.Stacks(reward.requires) <= 0)
            {
                Result required = GrantWithPrerequisites(reward.requires, report);
                if (!required.Ok) return Result.Fail($"{reward.displayName} kurulmadı — ön koşul kurulamadı: {required.Message}");
            }
            Result result = Grant(reward);
            report?.Add(result);
            return result;
        }

        // A) Normal kural testi: boss round'u oyunun round sonu akışıyla bitirilir. Bekleyen kart seçimleri varsa önce onlar açılır,
        // sonra oyunun kendi ödül ekranı. passTargets true: kota ve boss hasadı "geçilmiş" sayılır (değerlendirilmez).
        public static Result OpenNormalOffer(int bossRound, bool passTargets = true)
        {
            RoundManager rounds = RoundManager.Instance;
            BossRewardManager boss = Boss;
            if (rounds == null || boss == null) return Result.Fail("Round ya da ödül yöneticisi yok.");
            if (boss.Pool == null) return Result.Fail("Bu profilin boss ödül havuzu yok.");
            if (boss.IsPending) return Result.Fail("Zaten bekleyen bir boss teklifi var.");
            if (!rounds.IsBossRound(bossRound)) return Result.Fail($"R{bossRound} bir boss round'u değil. Boss round'ları: {BossRounds(rounds)}.");
            boss.EditorAllowOfferAgain();
            if (rounds.CurrentRound != bossRound || !rounds.IsRoundActive)
                if (!rounds.EditorJumpToRound(bossRound)) return Result.Fail($"R{bossRound} başlatılamadı.");
            if (!rounds.EditorEndRound(!passTargets)) return Result.Fail("Round bitirilemedi (aktif round yok).");
            TestLog.Note(TestLog.Kind.Reward, $"Normal teklif akışı: R{bossRound} bitti" + (passTargets ? " (hedefler geçilmiş sayıldı)" : " (gerçek hedef değerlendirmesi)"));
            return Result.Success($"R{bossRound} oyunun round sonu akışıyla bitti. " +
                                  (rounds.PendingCardSelections > 0 ? $"Önce {rounds.PendingCardSelections} kart seçimi, sonra " : "") +
                                  "oyunun ödül ekranı açılır; seçimi Game görünümünde yap.");
        }

        public static string BossRounds(RoundManager rounds)
        {
            var text = new StringBuilder();
            for (int round = 1; round <= rounds.MaxRounds; round++)
                if (rounds.IsBossRound(round)) text.Append(text.Length > 0 ? ", " : "").Append(round);
            return text.Length > 0 ? text.ToString() : "yok";
        }

        public static int NextBossRound(RoundManager rounds, int from) => rounds != null ? rounds.Calendar.NextBossRound(from) : 0;
    }
}
