using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MechanicsTestLab
{
    // Bir ödül kurulumunun makbuzu: istenen ödül, sonuç, oyunun kendi kazanç / bedel metni ve ÖLÇÜLEN gerçek değişim.
    // Değişim, kurulumdan hemen önce ve hemen sonra alınan iki görüntünün farkıdır (oyuncu + bir referans saksı); mekanik
    // ödüllerinde oyunun kendi kayıt / aktiflik durumu okunur. Durumdan bağımsız bir "uygulandı" varsayımı yapılmaz:
    // ölçülebilir bir değişim ya da aktiflik kaydı bulunmazsa makbuz bunu açıkça yazar.
    public sealed class RewardReceipt
    {
        public string Id = "", Name = "", Verdict = "", Refusal = "", ReferencePlanter = "";
        public int StacksBefore, StacksAfter, MaxStacks, ChoicesBefore, ChoicesAfter, Round;
        public double GameTime;
        public bool Success;
        public readonly List<string> Gains = new(), Costs = new(), Mechanics = new(), NewlyExcluded = new();
        public List<DiffRow> Changes = new();

        private BossRewardSO reward;
        private NumericSnapshot before;
        private GridPosition? reference;
        private MechanicState stateBefore;
        private readonly List<string> excludedBefore = new();

        private readonly struct MechanicState
        {
            public readonly bool Rhythm, Chain, EchoExplosion, EchoElectric, Effect;
            public readonly int MovingBonus, LevelChoices;
            public readonly string EffectState;

            public MechanicState(BossRewardSO reward)
            {
                Rhythm = RunPower.Rhythm.Enabled;
                Chain = BossRewardManager.TryGetChain(out _);
                EchoExplosion = RunPower.TryGetEcho(DamageType.Explosion, out _);
                EchoElectric = RunPower.TryGetEcho(DamageType.Electric, out _);
                RunEffects effects = RunEffects.Instance;
                Effect = reward.effect != null && effects != null && effects.Has(reward.effect);
                MovingBonus = MovingBehaviorLimits.Bonus;
                LevelChoices = BossRewardManager.LevelChoiceDelta;
                EffectState = "";
                if (!Effect) return;
                foreach (RunEffect effect in effects.Active)
                {
                    if (effect.Data != reward.effect) continue;
                    var text = new StringBuilder();
                    effect.Describe(text);
                    EffectState = text.ToString().Trim().Replace("\n", " · ");
                }
            }
        }

        // Referans saksı: davranışı olan ilk saksı, yoksa ilk saksı (saksı kapsamlı statların gerçek değişimini görmek için).
        private static GridPosition? Reference()
        {
            var planters = new List<PlanterBrain>();
            TestField.Planters(planters);
            PlanterBrain chosen = null;
            foreach (PlanterBrain brain in planters)
            {
                chosen ??= brain;
                if (RunEffects.BestBehaviorChance(brain) > 0f) { chosen = brain; break; }
            }
            return chosen != null && chosen.OccupiedGrids.Count > 0 ? chosen.OccupiedGrids[0].GetGridPosition() : (GridPosition?)null;
        }

        private static void Excluded(List<string> into)
        {
            into.Clear();
            BossRewardManager boss = BossRewardManager.Instance;
            var all = new List<BossRewardSO>();
            TestRewards.Catalog(all);
            foreach (BossRewardSO other in all)
                if (boss.BlockOf(other, false) == BossRewardBlock.Exclusive) into.Add(other.displayName);
        }

        public static RewardReceipt Begin(BossRewardSO reward)
        {
            BossRewardManager boss = BossRewardManager.Instance;
            RoundManager rounds = RoundManager.Instance;
            var receipt = new RewardReceipt
            {
                reward = reward, Id = reward.id, Name = reward.displayName, MaxStacks = reward.maxStacks, StacksBefore = boss.Stacks(reward),
                ChoicesBefore = rounds != null ? rounds.ChoicesPerLevel : 0, Round = rounds != null ? rounds.CurrentRound : 0, GameTime = Time.timeAsDouble,
            };
            receipt.reference = Reference();
            receipt.ReferencePlanter = receipt.reference.HasValue ? $"({receipt.reference.Value.x}, {receipt.reference.Value.z})" : "yok";
            receipt.before = Snapshots.Capture(receipt.reference, "kurulumdan önce");
            receipt.stateBefore = new MechanicState(reward);
            Excluded(receipt.excludedBefore);
            // Oyunun kendi kart metni: kazanç ve bedel satırları (ödül verisinden, o anki seçim hakkıyla).
            BossRewardText.TradeLines(reward, receipt.ChoicesBefore, receipt.Gains, receipt.Costs);
            if (receipt.Gains.Count == 0 && receipt.Costs.Count == 0) receipt.Gains.Add(TestInspector.Plain(BossRewardText.Effect(reward)));
            for (int i = 0; i < receipt.Gains.Count; i++) receipt.Gains[i] = TestInspector.Plain(receipt.Gains[i]);
            for (int i = 0; i < receipt.Costs.Count; i++) receipt.Costs[i] = TestInspector.Plain(receipt.Costs[i]);
            return receipt;
        }

        public void Finish(Result result)
        {
            BossRewardManager boss = BossRewardManager.Instance;
            RoundManager rounds = RoundManager.Instance;
            Success = result.Ok;
            Refusal = result.Ok ? "" : result.Message;
            StacksAfter = boss != null ? boss.Stacks(reward) : StacksBefore;
            ChoicesAfter = rounds != null ? rounds.ChoicesPerLevel : ChoicesBefore;
            NumericSnapshot after = Snapshots.Capture(reference, "kurulumdan sonra");
            Changes = Snapshots.Compare(before, after, true);
            before = null;
            if (!Success)
            {
                Verdict = Changes.Count == 0 ? "REDDEDİLDİ: hiçbir değer değişmedi." : "REDDEDİLDİ ama bazı değerler değişti: İNCELE.";
                TestLog.Note(TestLog.Kind.Reward, $"Ödül makbuzu: {Name} reddedildi — {Refusal}");
                return;
            }
            var state = new MechanicState(reward);
            if (state.Rhythm && !stateBefore.Rhythm) Mechanics.Add($"Hasat Ritmi kaydı oyunda etkin: eşik {RunPower.Rhythm.Threshold} doğrudan hasat · sayaç {RunPower.Rhythm.Count} · koşullu etki, henüz tetiklenmedi");
            if (state.Chain && !stateBefore.Chain) Mechanics.Add("Davranış zinciri oyunda etkin (BossRewardManager.TryGetChain): davranış hasatları zincir kararı üretir · koşullu etki, henüz tetiklenmedi");
            if (state.EchoExplosion && !stateBefore.EchoExplosion && RunPower.TryGetEcho(DamageType.Explosion, out BehaviorEcho explosion))
                Mechanics.Add($"Patlama ikinci darbesi oyunda etkin: gecikme {explosion.Delay:0.###} sn · hasar oranı {explosion.Damage:0.###} · yarıçap oranı {explosion.Radius:0.###} · koşullu etki, henüz tetiklenmedi");
            if (state.EchoElectric && !stateBefore.EchoElectric && RunPower.TryGetEcho(DamageType.Electric, out BehaviorEcho electric))
                Mechanics.Add($"Elektrik ikinci dalgası oyunda etkin: gecikme {electric.Delay:0.###} sn · hasar oranı {electric.Damage:0.###} · erişim {electric.Reach} hücre · koşullu etki, henüz tetiklenmedi");
            if (state.Effect && !stateBefore.Effect)
                Mechanics.Add("Çalışma zamanı etkisi oyunda kayıtlı (RunEffects): " + (reward.effect != null ? reward.effect.name : "?") + (state.EffectState.Length > 0 ? " · durum: " + state.EffectState : ""));
            if (state.MovingBonus != stateBefore.MovingBonus)
                Mechanics.Add($"Kasırga / bumerang aktif sınırı bonusu: {stateBefore.MovingBonus} → {state.MovingBonus}");
            if (state.LevelChoices != stateBefore.LevelChoices)
                Mechanics.Add($"Level başına seçim hakkı: {ChoicesBefore} → {ChoicesAfter} (yalnız bundan sonra kazanılan level'lar; bekleyen seçimler değişmedi)");
            var excludedNow = new List<string>();
            Excluded(excludedNow);
            foreach (string name in excludedNow) if (!excludedBefore.Contains(name)) NewlyExcluded.Add(name);

            bool stacked = StacksAfter == StacksBefore + 1;
            if (!stacked) Verdict = $"BEKLENMEYEN: stack {StacksBefore} → {StacksAfter} (bir artmalıydı): İNCELE.";
            else if (Changes.Count > 0 && Mechanics.Count > 0) Verdict = "KURULDU: ölçülen değer değişimi ve oyundaki mekanik kaydı aşağıda.";
            else if (Changes.Count > 0) Verdict = "KURULDU: ölçülen gerçek değer değişimi aşağıda.";
            else if (Mechanics.Count > 0) Verdict = "KURULDU: koşullu / mekanik etki oyunda kayıtlı; stat hemen değişmez, etkisi tetiklenince görülür (hata değildir).";
            else Verdict = "KURULDU ama ölçülen hiçbir değer ya da mekanik kaydı değişmedi: etkisi bu görüntünün kapsamadığı bir yerde olabilir (ör. saksısız tarla) — İNCELE.";
            TestLog.Note(TestLog.Kind.Reward, $"Ödül makbuzu: {Name} · stack {StacksBefore} → {StacksAfter}/{MaxStacks} · {Changes.Count} değer değişti · {Mechanics.Count} mekanik kaydı");
        }

        public string ToText()
        {
            var text = new StringBuilder();
            text.Append($"ÖDÜL MAKBUZU · {Name} [{Id}] · R{Round} · {GameTime:0.00} sn\n");
            text.Append($"İstenen: 1 stack ({StacksBefore} → {StacksBefore + 1} / azami {MaxStacks})\n");
            text.Append("Sonuç: ").Append(Success ? "BAŞARILI" : "REDDEDİLDİ").Append(Success ? $" · stack şimdi {StacksAfter}/{MaxStacks}" : " · neden: " + Refusal).Append('\n');
            text.Append(Verdict).Append('\n');
            text.Append("Oyunun kart metni — kazanç: ").Append(Gains.Count > 0 ? string.Join(" · ", Gains) : "—").Append('\n');
            text.Append("Oyunun kart metni — bedel: ").Append(Costs.Count > 0 ? string.Join(" · ", Costs) : "yok").Append('\n');
            text.Append("Ölçülen değişim (oyuncu + referans saksı ").Append(ReferencePlanter).Append("):\n");
            if (Changes.Count == 0) text.Append("   değişen değer yok\n");
            foreach (DiffRow row in Changes)
            {
                text.Append("   ").Append(row.Label).Append(" [").Append(row.Scope).Append("] ").Append(row.Before).Append(" → ").Append(row.After);
                if (row.Delta.Length > 0) text.Append(" · fark ").Append(row.Delta);
                if (row.Ratio.Length > 0) text.Append(" · oran ").Append(row.Ratio);
                if (row.Source.Length > 0) text.Append(" · kaynak ").Append(row.Source);
                text.Append('\n');
            }
            text.Append("Mekanik kaydı:\n");
            if (Mechanics.Count == 0) text.Append("   yeni mekanik kaydı yok\n");
            foreach (string line in Mechanics) text.Append("   ").Append(line).Append('\n');
            text.Append("Dışlama grubuna etkisi: ").Append(NewlyExcluded.Count > 0 ? "artık kurulamaz → " + string.Join(", ", NewlyExcluded) : "yeni dışlanan ödül yok").Append('\n');
            text.Append("Gelecek level seçim hakkına etkisi: ").Append(ChoicesBefore == ChoicesAfter ? $"yok ({ChoicesAfter})" : $"{ChoicesBefore} → {ChoicesAfter}").Append('\n');
            return text.ToString();
        }
    }
}
