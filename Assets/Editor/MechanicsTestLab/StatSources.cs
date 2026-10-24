using System.Collections.Generic;
using UnityEngine;

namespace MechanicsTestLab
{
    public enum SourceKind { Base, Loadout, Skill, Specialization, BasePowerCards, Tile, Resonance, BossReward, BossRule, Temporary, Test, Unknown }

    // Bir stat'a (ya da doğrudan vuruş / davranış hasarı gibi türetilmiş bir değere) uygulanmış TEK gerçek katkı.
    // Değerler oyunun kendi listelerinden okunur; hiçbir katkı tahminle doldurulmaz.
    public sealed class StatSource
    {
        public SourceKind Kind;
        public string Name = "";
        public StatType Stat;
        // Stat modifier'ı mı (işlem + değer), yoksa stat dışı bir çarpan mı (doğrudan vuruş, davranış hasarı …).
        public bool IsModifier = true;
        public ModifierOperation Operation;
        public float Value;
        public string Scope = "";
        public int Stacks = 1;
        // Katkının hangi şartla aktif olduğu (boş: koşulsuz) ve geçiciyse kalan süresi (−1: süresiz).
        public string Condition = "";
        public float Remaining = -1f;

        public string KindText => Kind switch
        {
            SourceKind.Base => "taban",
            SourceKind.Loadout => "başlangıç seçimi",
            SourceKind.Skill => "skill",
            SourceKind.Specialization => "uzmanlaşma",
            SourceKind.BasePowerCards => "temel güç kartları",
            SourceKind.Tile => "tile",
            SourceKind.Resonance => "rezonans",
            SourceKind.BossReward => "boss ödülü",
            SourceKind.BossRule => "boss kuralı",
            SourceKind.Temporary => "geçici etki",
            SourceKind.Test => "TEST KATKISI",
            _ => "kaynağı kayıtlı değil",
        };

        public string Effect
        {
            get
            {
                if (Kind == SourceKind.Base) return Value.ToString("0.####");
                if (!IsModifier) return "×" + Value.ToString("0.####") + " (çarpımsal)";
                return Operation switch
                {
                    ModifierOperation.Flat => (Value >= 0 ? "+" : "") + Value.ToString("0.####") + " (toplamsal, düz)",
                    ModifierOperation.AddPercent => (Value >= 0 ? "+" : "") + (Value * 100f).ToString("0.##") + "% (toplamsal yüzde)",
                    ModifierOperation.MorePercent => "×" + (1f + Value).ToString("0.####") + " (çarpımsal)",
                    _ => "= " + Value.ToString("0.####") + " (tabanın yerine geçer)",
                };
            }
        }

        public string Describe()
        {
            string text = $"[{KindText}] {Name}: {Effect}";
            if (Stacks > 1) text += $" · stack {Stacks}";
            if (!string.IsNullOrEmpty(Scope)) text += " · kapsam: " + Scope;
            if (!string.IsNullOrEmpty(Condition)) text += " · koşul: " + Condition;
            if (Remaining >= 0f) text += $" · kalan {Remaining:0.0} sn";
            return text;
        }

        // Önce / sonra karşılaştırmasında "hangi kaynak eklendi" sorusu için kimlik (değer dahil).
        public string Identity => KindText + ": " + Name + (Stacks > 1 ? " ×" + Stacks : "");
    }

    // Seçili hedefte uygulanmış gerçek katkıların kaynaklarına ayrılması.
    // Global modifier'ları oyunun StatManager'ı kaynaksız tek listede tutar; burada her biri, onu eklediğini BİLDİĞİMİZ sistemin
    // kendi kaydıyla eşleştirilir (çoklu küme eşleşmesi): test katkıları, boss ödülleri, başlangıç seçimi, satın alınmış skill
    // seviyeleri, toplanan temel güç kartı grubu, aktif Nadir Reaktör ve aktif Sis. Hiçbiriyle eşleşmeyen "kaynağı kayıtlı değil"
    // kalır (bugünkü oyunda bu, toplanmayan temel güç kartıdır: aralık kartları).
    public static class StatSources
    {
        public const string OrderNote =
            "Liste kaynak türüne göre sıralıdır, hesap sırası DEĞİLDİR. Oyunun hesabı: (taban ya da 'tabanın yerine geçen' değer + düz katkılar) " +
            "× (1 + toplanan yüzdeler) × ayrı çarpanların çarpımı, sonra sınır. Saksıda rezonans ayrı ikinci aşamadır (stat başına en güçlü etki).";

        private static readonly StatType[] ReactorChances = { StatType.ExplosionChance, StatType.TornadoChance, StatType.BoomerangChance, StatType.ElectricChance };

        // brain null: oyuncu / global kapsam (target). Dolu: o saksının kapsamı (taban, global Planter katkıları, tile, rezonans).
        public static void Collect(StatType stat, StatTarget target, PlanterBrain brain, List<StatSource> into)
        {
            into.Clear();
            StatManager stats = StatManager.Instance;
            if (stats == null) return;
            bool planterScope = brain != null;
            StatTarget context = planterScope ? StatTarget.Planter : target;
            string scope = planterScope ? $"saksı ({brain.EditorOrigin.x}, {brain.EditorOrigin.z})" : context.ToString();

            if (planterScope || stats.HasBaseStat(stat))
                into.Add(new StatSource
                {
                    Kind = SourceKind.Base, Stat = stat, Scope = scope,
                    Name = planterScope ? (RunBalanceSO.TryPlanterBaseStat(stat, out _) ? "profilin denge seti" : "saksı verisi / varsayılan") : "temel stat seti",
                    Value = planterScope ? brain.EditorData.GetBaseStat(stat) : stats.GetBaseStat(stat),
                });

            // Global modifier'lar: bilinen sahiplerin kayıtlarıyla eşleştirilir.
            var remaining = new List<StatModifier>();
            foreach (StatModifier modifier in stats.GlobalModifiers)
                if (modifier.statType == stat && (modifier.target == StatTarget.All || modifier.target == context)) remaining.Add(modifier);

            foreach (StatModifier modifier in TestStats.Contributions)
                Claim(remaining, modifier, into, SourceKind.Test, "test katkısı", scope, "", -1f);

            BossRewardManager boss = BossRewardManager.Instance;
            if (boss != null)
                foreach (BossRewardSO reward in boss.Taken)
                    for (int i = 0; i < boss.Stacks(reward); i++)
                        foreach (StatModifier modifier in reward.modifiersPerStack)
                            Claim(remaining, modifier, into, SourceKind.BossReward, reward.displayName, scope, "", -1f);

            StartLoadoutManager loadout = StartLoadoutManager.Instance;
            if (loadout != null)
                foreach (StartOptionSO option in new StartOptionSO[] { loadout.Farmer, loadout.Scythe })
                    if (option != null)
                        foreach (StatModifier modifier in option.modifiers)
                            Claim(remaining, modifier, into, SourceKind.Loadout, option.displayName, scope, "", -1f);

            SkillTreeManager tree = SkillTreeManager.Instance;
            if (tree != null && tree.AllNodes != null)
                foreach (SkillNodeSO node in tree.AllNodes)
                {
                    int level = node != null ? tree.GetCurrentLevel(node) : 0;
                    if (level <= 0 || level > node.tiers.Count) continue;
                    foreach (StatModifier modifier in node.tiers[level - 1].effects)
                        Claim(remaining, modifier, into, SourceKind.Skill, $"{node.nodeName} Sv {level}", scope, "", -1f);
                }

            RunEffects effects = RunEffects.Instance;
            RareReactorEffect reactor = effects != null ? effects.Find<RareReactorEffect>() : null;
            if (reactor != null && reactor.IsActive && reactor.Data is RareReactorEffectSO reactorData && System.Array.IndexOf(ReactorChances, stat) >= 0)
                Claim(remaining, new StatModifier { statType = stat, target = StatTarget.Planter, operation = ModifierOperation.MorePercent, value = reactorData.chanceBonus },
                    into, SourceKind.Temporary, "Nadir Reaktör", scope, $"nadir bitkinin doğrudan hasadı (yenileme {reactor.Refreshes})", reactor.Remaining);

            float summed = stats.SummedGroupTotal(stat, context);
            if (!Mathf.Approximately(summed, 0f))
                Claim(remaining, new StatModifier { statType = stat, target = context, operation = ModifierOperation.MorePercent, value = summed },
                    into, SourceKind.BasePowerCards, "toplanan temel güç kartı grubu", scope, "", -1f);

            // Sis: boss kuralı vuruş yarıçapına kendi çarpanını ekler; değeri kuralın içindedir. Aktifken eşleşmeyen tek yarıçap
            // çarpanı ona aittir (birden fazlaysa ayrılamaz ve "kayıtlı değil" kalır).
            SegmentEventRuntime active = SegmentEventDirector.Instance != null ? SegmentEventDirector.Instance.Active : null;
            if (active is FogEvent && stat == StatType.AreaRadius && remaining.Count == 1 && remaining[0].operation == ModifierOperation.MorePercent)
                Claim(remaining, remaining[0], into, SourceKind.BossRule, active.Data.displayName, scope, "boss round'u boyunca", -1f);

            foreach (StatModifier modifier in remaining)
                Add(into, new StatSource
                {
                    Kind = SourceKind.Unknown, Name = "global modifier", Stat = stat, Operation = modifier.operation, Value = modifier.value, Scope = scope,
                    Condition = "oyun bu katkının kaynağını tutmuyor (toplanmayan temel güç kartı olabilir)",
                });

            if (!planterScope) return;
            foreach (GridObject grid in brain.OccupiedGrids)
            {
                GroundCell ground = grid != null ? grid.GetGroundCellCached() : null;
                if (ground == null || ground.CurrentModifier == null) continue;
                GridPosition cell = grid.GetGridPosition();
                foreach (StatModifier modifier in ground.RolledModifiers)
                    if (modifier.statType == stat && (modifier.target == StatTarget.All || modifier.target == StatTarget.Planter))
                        into.Add(new StatSource
                        {
                            Kind = SourceKind.Tile, Name = $"{ground.CurrentModifier.name} Sv {ground.Level} @ ({cell.x}, {cell.z})", Stat = stat,
                            Operation = modifier.operation, Value = modifier.value, Scope = scope, Condition = "saksının altındaki hücre",
                        });
            }
            foreach (StatModifier modifier in brain.EditorResonanceModifiers)
            {
                if (modifier.statType != stat) continue;
                into.Add(new StatSource
                {
                    Kind = SourceKind.Resonance, Name = ResonanceName(brain, stat), Stat = stat, Operation = modifier.operation, Value = modifier.value, Scope = scope,
                    Condition = "tile şartı sağlandıkça (ayrı aşama; stat başına en güçlü etki)",
                });
            }
        }

        // Stat'a etki eden etkin rezonansların adları ve şartları (oyunun değerlendirmesinden).
        private static string ResonanceName(PlanterBrain brain, StatType stat)
        {
            var text = new System.Text.StringBuilder();
            foreach (ActiveResonance resonance in brain.ActiveResonances)
                foreach (ResonanceEffect effect in resonance.effects)
                {
                    bool touches = (effect.kind == ResonanceEffectKind.Stat && effect.statType == stat) ||
                                   (effect.kind == ResonanceEffectKind.Resources && (stat == StatType.GoldGainMultiplier || stat == StatType.IronGainMultiplier || stat == StatType.StoneGainMultiplier));
                    if (!touches) continue;
                    if (text.Length > 0) text.Append(" / ");
                    text.Append(resonance.resonanceName).Append(" (").Append(resonance.tileType).Append(' ').Append(resonance.tileCount).Append('/').Append(resonance.requiredCount).Append(')');
                    break;
                }
            return text.Length > 0 ? text.ToString() : "rezonans";
        }

        private static void Claim(List<StatModifier> remaining, StatModifier modifier, List<StatSource> into, SourceKind kind, string name, string scope,
            string condition, float remainingSeconds)
        {
            int index = TestStats.IndexOf(remaining, modifier);
            if (index < 0) return;
            remaining.RemoveAt(index);
            Add(into, new StatSource
            {
                Kind = kind, Name = name, Stat = modifier.statType, Operation = modifier.operation, Value = modifier.value, Scope = scope, Condition = condition,
                Remaining = remainingSeconds,
            });
        }

        // Aynı kaynağın aynı katkısı tek satırda, stack sayısıyla.
        private static void Add(List<StatSource> into, StatSource source)
        {
            foreach (StatSource existing in into)
                if (existing.Kind == source.Kind && existing.Name == source.Name && existing.IsModifier == source.IsModifier &&
                    existing.Operation == source.Operation && Mathf.Approximately(existing.Value, source.Value))
                {
                    existing.Stacks++;
                    return;
                }
            into.Add(source);
        }

        // ---------------------------------------------------------------- stat dışı çarpanlar
        // Doğrudan vuruşun stat dışındaki gerçek çarpanları (oyunun tek çarpımı: RunPower.DirectDamage). rarity: hedefin nadirliği.
        public static void CollectDirect(PlantRarity rarity, List<StatSource> into)
        {
            into.Clear();
            Multiplier(into, SourceKind.Specialization, "doğrudan vuruş", SpecializationManager.DirectMultiplier, "oyuncunun doğrudan vuruşu", "");
            StartLoadoutManager loadout = StartLoadoutManager.Instance;
            if (loadout != null)
                foreach (StartOptionSO option in new StartOptionSO[] { loadout.Farmer, loadout.Scythe })
                    if (option != null) Multiplier(into, SourceKind.Loadout, option.displayName, option.directDamageMultiplier, "oyuncunun doğrudan vuruşu", "");
            BossRewardManager boss = BossRewardManager.Instance;
            if (boss == null) return;
            foreach (BossRewardSO reward in boss.Taken)
            {
                int stacks = boss.Stacks(reward);
                if (!Mathf.Approximately(reward.directDamageMultiplier, 1f))
                    Multiplier(into, SourceKind.BossReward, reward.displayName, Mathf.Pow(reward.directDamageMultiplier, stacks), "oyuncunun doğrudan vuruşu", "", stacks);
                if (!Mathf.Approximately(reward.rareDirectMultiplier, 1f) && rarity >= reward.rareDirectFrom)
                    Multiplier(into, SourceKind.BossReward, reward.displayName, Mathf.Pow(reward.rareDirectMultiplier, stacks), "oyuncunun doğrudan vuruşu",
                        $"hedef {reward.rareDirectFrom} ve üstü (bu hedef: {rarity})", stacks);
            }
        }

        // Bir saksının davranış hasarının gerçek çarpanları (oyunun tek hesabı: PlanterBrain.BehaviorDamageMultiplier).
        public static void CollectBehavior(PlanterBrain brain, DamageType type, List<StatSource> into)
        {
            into.Clear();
            if (brain != null)
                Multiplier(into, SourceKind.Resonance, "davranış rezonansı", ResonanceManager.BehaviorMultiplier(brain.ActiveResonances, type),
                    $"saksı ({brain.EditorOrigin.x}, {brain.EditorOrigin.z})", "davranış tile'ı şartı sağlandıkça");
            Multiplier(into, SourceKind.Specialization, "davranış hasarı", SpecializationManager.BehaviorMultiplier, "dört davranış", "");
            BossRewardManager boss = BossRewardManager.Instance;
            if (boss == null) return;
            foreach (BossRewardSO reward in boss.Taken)
                if (!Mathf.Approximately(reward.behaviorDamageMultiplier, 1f))
                    Multiplier(into, SourceKind.BossReward, reward.displayName, Mathf.Pow(reward.behaviorDamageMultiplier, boss.Stacks(reward)), "dört davranış", "", boss.Stacks(reward));
        }

        private static void Multiplier(List<StatSource> into, SourceKind kind, string name, float value, string scope, string condition, int stacks = 1)
        {
            if (Mathf.Approximately(value, 1f)) return;
            into.Add(new StatSource { Kind = kind, Name = name, IsModifier = false, Value = value, Scope = scope, Condition = condition, Stacks = stacks });
        }
    }
}
