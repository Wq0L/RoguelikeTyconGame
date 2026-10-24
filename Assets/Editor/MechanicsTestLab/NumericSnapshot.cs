using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MechanicsTestLab
{
    public enum ValueUnit { Number, Damage, Chance, Multiplier, Seconds, Count, Health, Points }

    // Görüntüdeki tek satır. Yalnız sayı ve metin taşır (canlı nesne referansı yok): sonradan oyun değişse de satır değişmez.
    public sealed class SnapshotRow
    {
        public string Key = "", Label = "", Scope = "", Context = "", Limit = "";
        public ValueUnit Unit;
        public bool HasValue = true;
        public double Value;
        // Sayısal olmayan durum (ör. "Hasat Ritmi: 3/5").
        public string Text = "";
        // Ham taban mı (oyun verisi), yoksa bağlama göre çözülmüş gerçek değer mi.
        public bool Raw;
        // Görüntü anındaki katkı kaynaklarının kimlikleri (metin kopyası).
        public List<string> Sources = new();
    }

    public sealed class NumericSnapshot
    {
        public string Title = "", Target = "";
        public int Round, Frame;
        public double GameTime;
        public readonly List<SnapshotRow> Rows = new();
    }

    public sealed class DiffRow
    {
        public string Label = "", Scope = "", Before = "", After = "", Delta = "", Ratio = "", Source = "", Unit = "", Note = "";
        public bool Changed;
    }

    // Önce / sonra stat karşılaştırması. Bütün değerler oyunun çözülmüş değerleridir (StatManager / PlanterBrain / PlantSpawner /
    // PlantHealth ve oyunun kendi hasar çarpımı); burada stat formülü yoktur. Türetilmiş satırlar (ör. "sapmasız doğrudan vuruş")
    // oyunun aynı fonksiyonunun belirli bir bağlamla çağrılmasıdır ve bağlamı satırda yazar; gerçek bir saldırının sonucu
    // için "Saldırı izi" kullanılır.
    public static class Snapshots
    {
        private static readonly List<StatSource> sources = new();
        private static readonly DamageType[] Behaviors = { DamageType.Explosion, DamageType.Electric, DamageType.Tornado, DamageType.Boomerang };
        private static readonly StatType[] Chances = { StatType.ExplosionChance, StatType.ElectricChance, StatType.TornadoChance, StatType.BoomerangChance };
        private static readonly string[] BehaviorNames = { "Patlama", "Elektrik", "Kasırga", "Bumerang" };

        public static ValueUnit UnitOf(StatType stat) => stat switch
        {
            StatType.HarvestDamage => ValueUnit.Damage,
            StatType.CritChance or StatType.ExplosionChance or StatType.ElectricChance or StatType.TornadoChance or StatType.BoomerangChance
                or StatType.DuplicateChance or StatType.MutationLuck => ValueUnit.Chance,
            StatType.CritMultiplier or StatType.PlanterDamageMultiplier or StatType.XPGainMultiplier or StatType.GoldGainMultiplier
                or StatType.IronGainMultiplier or StatType.StoneGainMultiplier or StatType.HarvestScoreMultiplier => ValueUnit.Multiplier,
            StatType.AttackSpeed or StatType.PlantSpawnRate => ValueUnit.Seconds,
            StatType.RareSpawnChance => ValueUnit.Points,
            _ => ValueUnit.Number,
        };

        // Oyuncu + (varsa) seçili hücrenin saksısı, bitkisi ve tile'ı.
        public static NumericSnapshot Capture(GridPosition? cell, string title)
        {
            var snapshot = new NumericSnapshot
            {
                Title = title, Frame = Time.frameCount, GameTime = Time.timeAsDouble,
                Round = RoundManager.Instance != null ? RoundManager.Instance.CurrentRound : 0,
                Target = cell.HasValue ? $"oyuncu + hücre ({cell.Value.x}, {cell.Value.z})" : "oyuncu",
            };
            CapturePlayer(snapshot);
            if (cell.HasValue) CaptureCell(snapshot, cell.Value);
            return snapshot;
        }

        private static SnapshotRow Add(NumericSnapshot snapshot, string key, string label, string scope, ValueUnit unit, double value, string context = "", string limit = "")
        {
            var row = new SnapshotRow { Key = key, Label = label, Scope = scope, Unit = unit, Value = value, Context = context, Limit = limit };
            snapshot.Rows.Add(row);
            return row;
        }

        private static void AddText(NumericSnapshot snapshot, string key, string label, string scope, string text) =>
            snapshot.Rows.Add(new SnapshotRow { Key = key, Label = label, Scope = scope, HasValue = false, Text = text });

        private static void CopySources(SnapshotRow row)
        {
            foreach (StatSource source in sources)
                if (source.Kind != SourceKind.Base) row.Sources.Add(source.Identity);
        }

        private static string Limit(StatType stat)
        {
            float low = StatCalculator.ClampStat(stat, float.MinValue), high = StatCalculator.ClampStat(stat, float.MaxValue);
            if (low <= float.MinValue && high >= float.MaxValue) return "";
            return "sınır: " + (low > float.MinValue ? "alt " + low.ToString("0.####") : "") + (low > float.MinValue && high < float.MaxValue ? " · " : "") +
                   (high < float.MaxValue ? "üst " + high.ToString("0.####") : "");
        }

        private static void CapturePlayer(NumericSnapshot snapshot)
        {
            StatManager stats = StatManager.Instance;
            if (stats == null) return;
            foreach ((StatType stat, StatTarget target, string label) in TestStats.Offered)
            {
                if (target == StatTarget.Planter || !stats.HasBaseStat(stat)) continue;
                SnapshotRow raw = Add(snapshot, "p.raw." + stat, label + " — ham taban", "oyuncu", UnitOf(stat), stats.GetBaseStat(stat), "temel stat setindeki değer");
                raw.Raw = true;
                SnapshotRow row = Add(snapshot, "p." + stat, label, "oyuncu", UnitOf(stat), stats.GetFinalStat(stat, target), "çözülmüş değer", Limit(stat));
                StatSources.Collect(stat, target, null, sources);
                CopySources(row);
            }

            float damage = stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player), crit = stats.GetFinalStat(StatType.CritMultiplier, StatTarget.Player);
            foreach (PlantRarity rarity in new[] { PlantRarity.Common, PlantRarity.Rare })
            {
                int direct = NumericSafety.ToInt(RunPower.DirectDamage(damage, 1f, rarity, 1f), 1, NumericSite.DirectDamage);
                SnapshotRow row = Add(snapshot, "p.direct." + rarity, $"Doğrudan vuruş hasarı ({rarity} hedef)", "oyuncu", ValueUnit.Damage, direct,
                    "oyunun doğrudan vuruş çarpımı; sapma ×1 (gerçek vuruş ×0,85–1,15), Hasat Ritmi / Açgözlü Hasat çarpanı ve saksı bonusu hariç", "tek yuvarlama; en az 1");
                StatSources.Collect(StatType.HarvestDamage, StatTarget.Player, null, sources);
                CopySources(row);
                StatSources.CollectDirect(rarity, sources);
                CopySources(row);
                if (rarity != PlantRarity.Common) continue;
                Add(snapshot, "p.directcrit", "Kritik doğrudan vuruş hasarı (Common hedef)", "oyuncu", ValueUnit.Damage,
                    NumericSafety.ToInt(direct * crit, 0, NumericSite.CritDamage), "yukarıdaki vuruş × kritik çarpanı; ikinci yuvarlama");
            }
            Add(snapshot, "p.directmult", "Doğrudan vuruş · boss ödülü çarpanı", "oyuncu", ValueUnit.Multiplier, BossRewardManager.DirectDamageMultiplier, "stat değil; yalnız doğrudan vuruşa");
            Add(snapshot, "p.raremult", "Doğrudan vuruş · Rare+ hedef çarpanı", "oyuncu", ValueUnit.Multiplier, BossRewardManager.RareDirectMultiplier(PlantRarity.Rare), "yalnız Rare ve üstü hedefe");
            Add(snapshot, "p.behaviormult", "Davranış hasarı · boss ödülü çarpanı", "dört davranış", ValueUnit.Multiplier, BossRewardManager.BehaviorDamageMultiplier, "stat değil; doğrudan vuruşa girmez");
            Add(snapshot, "p.interval", "Gerçek saldırı aralığı", "oyuncu", ValueUnit.Seconds, TestAttack.Interval,
                "stat + oyunun alt sınırı + round temposu", $"oyundaki alt sınır {PlayerController.MinAttackInterval:0.##} sn");
            RoundManager rounds = RoundManager.Instance;
            if (rounds != null)
            {
                Add(snapshot, "p.choices", "Level başına kart seçim hakkı", "run", ValueUnit.Count, rounds.ChoicesPerLevel,
                    $"temel {rounds.BaseChoicesPerLevel} + ödül etkisi; yalnız bundan sonra kazanılan level'lar", $"geçerli aralık {RunPower.MinLevelChoices}–{RunPower.MaxLevelChoices}");
                Add(snapshot, "p.pending", "Bekleyen kart seçimi", "run", ValueUnit.Count, rounds.PendingCardSelections);
            }
            if (TornadoManager.Instance != null) Add(snapshot, "p.movinglimit", "Kasırga / bumerang aktif sınırı", "run", ValueUnit.Count, TornadoManager.Instance.ActiveLimit, "denge verisi + ödül bonusu");

            HarvestRhythm rhythm = RunPower.Rhythm;
            AddText(snapshot, "p.rhythm", "Hasat Ritmi", "oyuncu", !rhythm.Enabled ? "ödül yok" : rhythm.Ready ? "HAK HAZIR" : $"sayaç {rhythm.Count}/{rhythm.Threshold}");
            RunEffects effects = RunEffects.Instance;
            GreedyHarvestEffect greed = effects != null ? effects.Find<GreedyHarvestEffect>() : null;
            AddText(snapshot, "p.greed", "Açgözlü Hasat", "oyuncu", greed == null ? "ödül yok" : greed.Ready ? "HAK HAZIR" : "hak yok");
            RareReactorEffect reactor = effects != null ? effects.Find<RareReactorEffect>() : null;
            AddText(snapshot, "p.reactor", "Nadir Reaktör", "bütün saksılar", reactor == null ? "ödül yok" : reactor.IsActive ? $"AKTİF · kalan {reactor.Remaining:0.0} sn" : "beklemede");
            SegmentEventRuntime boss = SegmentEventDirector.Instance != null ? SegmentEventDirector.Instance.Active : null;
            AddText(snapshot, "p.boss", "Aktif boss kuralı", "tarla", boss != null ? $"{boss.Data.displayName} · {boss.RuleSummary}" : "yok");
        }

        private static void CaptureCell(NumericSnapshot snapshot, GridPosition cell)
        {
            string at = $"({cell.x}, {cell.z})";
            GroundCell ground = TestField.Ground(cell);
            if (ground == null) { AddText(snapshot, "c.tile", "Tile", "hücre " + at, "tarlanın dışında"); return; }
            TileModifierSO tile = ground.CurrentModifier;
            AddText(snapshot, "c.tile", "Tile", "hücre " + at, tile != null ? $"{tile.name} · Sv {ground.Level}" : "yok");
            if (tile != null)
                for (int i = 0; i < ground.RolledModifiers.Count; i++)
                {
                    StatModifier modifier = ground.RolledModifiers[i];
                    Add(snapshot, "c.tile." + i, $"Tile değeri · {modifier.statType} ({modifier.operation})", "hücre " + at, ValueUnit.Number, modifier.value, "seviye çarpanı dahil");
                }

            PlanterBrain brain = TestField.PlanterAt(cell);
            if (brain == null) { AddText(snapshot, "s.planter", "Saksı", "hücre " + at, "yok"); return; }
            string scope = $"saksı ({brain.EditorOrigin.x}, {brain.EditorOrigin.z})";
            AddText(snapshot, "s.planter", "Saksı", scope, brain.EditorData != null ? brain.EditorData.name : brain.name);
            var names = new StringBuilder();
            foreach (ActiveResonance resonance in brain.ActiveResonances)
                names.Append(names.Length > 0 ? ", " : "").Append(resonance.resonanceName).Append(' ').Append(resonance.tileCount).Append('/').Append(resonance.requiredCount);
            AddText(snapshot, "s.resonance", "Etkin rezonanslar", scope, names.Length > 0 ? names.ToString() : "yok");

            foreach ((StatType stat, StatTarget target, string label) in TestStats.Offered)
            {
                if (target != StatTarget.Planter) continue;
                SnapshotRow raw = Add(snapshot, "s.raw." + stat, label.Replace(" · bütün saksılar", "") + " — ham taban", scope, UnitOf(stat), brain.EditorData.GetBaseStat(stat), "saksı verisi / denge seti");
                raw.Raw = true;
                SnapshotRow row = Add(snapshot, "s." + stat, label.Replace(" · bütün saksılar", ""), scope, UnitOf(stat), brain.GetFinalStat(stat),
                    "çözülmüş değer (global + tile + rezonans)", Limit(stat));
                StatSources.Collect(stat, target, brain, sources);
                CopySources(row);
            }
            int baseDamage = PlanterBrain.HarvestDamage;
            for (int i = 0; i < Behaviors.Length; i++)
            {
                SnapshotRow multiplier = Add(snapshot, "s.behmult." + Behaviors[i], BehaviorNames[i] + " hasar çarpanı", scope, ValueUnit.Multiplier,
                    brain.BehaviorDamageMultiplier(Behaviors[i]), "rezonans × uzmanlaşma × boss ödülü (oyunun tek hesabı)");
                StatSources.CollectBehavior(brain, Behaviors[i], sources);
                CopySources(multiplier);
                // Patlama ve elektrikte vuruş hasarı taban × çarpandır; kasırga ve bumerang ayrıca kendi prefab katsayısını uygular
                // (o katsayı yalnız gerçek vuruşta, "Saldırı izi"nde görülür).
                if (Behaviors[i] == DamageType.Explosion || Behaviors[i] == DamageType.Electric)
                    Add(snapshot, "s.behdamage." + Behaviors[i], BehaviorNames[i] + " vuruş hasarı", scope, ValueUnit.Damage, brain.GetBehaviorDamage(baseDamage, Behaviors[i]),
                        $"taban (hasat hasarı stat'ı, {baseDamage}) × çarpan; tek yuvarlama; zincir nesli hariç");
            }
            Add(snapshot, "s.xp", "Hasat XP çarpanı (rezonans dahil)", scope, ValueUnit.Multiplier, brain.GetHarvestXP(1f), "oyunun hasat XP hesabı");
            Add(snapshot, "s.score.common", "Hasat skoru çarpanı (Common)", scope, ValueUnit.Multiplier, brain.GetHarvestScore(PlantRarity.Common), "saksının skor çarpanı");
            Add(snapshot, "s.score.rare", "Hasat skoru çarpanı (Rare)", scope, ValueUnit.Multiplier, brain.GetHarvestScore(PlantRarity.Rare), "saksının skor çarpanı");
            Add(snapshot, "s.rawinterval", "Üretim aralığı — tabandan önce", scope, ValueUnit.Seconds, brain.GetRawSpawnInterval(), "tabanın altındaki payı nadirliğe dönüşür",
                $"taban {StatCalculator.MinimumSpawnInterval:0.##} sn");
            Add(snapshot, "s.overflow", "Tabandan taşan nadirlik", scope, ValueUnit.Points, brain.GetSpawnOverflowRarity(), "üretim tabanı dönüşümü");

            PlantSpawner spawner = TestField.SpawnerAt(cell);
            if (spawner != null)
                Add(snapshot, "c.spawn", "Bu yuvanın gerçek üretim aralığı", "yuva " + at, ValueUnit.Seconds, spawner.EditorInterval, "taban, tempo, boss kuralı ve tarla olayı dahil (oyunun kullandığı değer)");
            PlantHealth plant = TestField.PlantAt(cell);
            if (plant == null || plant.Data == null) { AddText(snapshot, "c.plant", "Bitki", "yuva " + at, spawner != null ? "boş" : "yuva yok"); return; }
            AddText(snapshot, "c.plant", "Bitki", "yuva " + at, $"{plant.Data.name} [{plant.Data.rarity}]");
            Add(snapshot, "c.health", "Bitkinin mevcut canı", "yuva " + at, ValueUnit.Health, plant.CurrentHealth);
            Add(snapshot, "c.maxhealth", "Bitkinin azami canı", "yuva " + at, ValueUnit.Health, plant.MaxHealth, "oyunun can eğrisi × olay can çarpanı");
            StatManager stats = StatManager.Instance;
            int direct = NumericSafety.ToInt(RunPower.DirectDamage(stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player), 1f, plant.Data.rarity, 1f), 1, NumericSite.DirectDamage);
            Add(snapshot, "c.directhit", "Bu bitkiye doğrudan vuruş (sapmasız)", "yuva " + at, ValueUnit.Damage, plant.GetIncomingDamage(direct),
                $"hedef {plant.Data.rarity}; saksı hasar çarpanı dahil; sapma ×1");
        }

        // ---------------------------------------------------------------- biçim
        public static string Format(ValueUnit unit, double value) => unit switch
        {
            ValueUnit.Chance => (value * 100d).ToString("0.##") + "%",
            ValueUnit.Multiplier => "×" + value.ToString("0.####"),
            ValueUnit.Seconds => value.ToString("0.###") + " sn" + (value > 0d ? $" ({1d / value:0.##}/sn)" : ""),
            ValueUnit.Count or ValueUnit.Damage or ValueUnit.Health => value.ToString("0.##"),
            _ => value.ToString("0.####"),
        };

        public static string UnitName(ValueUnit unit) => unit switch
        {
            ValueUnit.Chance => "şans (yüzde)",
            ValueUnit.Multiplier => "çarpan",
            ValueUnit.Seconds => "saniye (parantezde saniyedeki sayı)",
            ValueUnit.Count => "adet",
            ValueUnit.Damage => "hasar",
            ValueUnit.Health => "can",
            ValueUnit.Points => "puan",
            _ => "sayı",
        };

        // Mutlak fark kendi biriminde: şans için YÜZDE PUANI, çarpan için çarpan farkı, süre için saniye.
        private static string FormatDelta(ValueUnit unit, double before, double after)
        {
            double delta = after - before;
            string sign = delta > 0d ? "+" : "";
            return unit switch
            {
                ValueUnit.Chance => sign + (delta * 100d).ToString("0.##") + " yüzde puanı",
                ValueUnit.Seconds => sign + delta.ToString("0.###") + " sn",
                ValueUnit.Multiplier => sign + delta.ToString("0.####") + " (çarpan farkı)",
                _ => sign + delta.ToString("0.####"),
            };
        }

        // Oransal fark: sonra ÷ önce. Önce 0 ise oran uydurulmaz.
        private static string FormatRatio(ValueUnit unit, double before, double after)
        {
            if (System.Math.Abs(before) < 1e-9) return System.Math.Abs(after) < 1e-9 ? "—" : "hesaplanamaz (önce 0)";
            double ratio = after / before;
            return "×" + ratio.ToString("0.####") + $" ({(ratio - 1d) * 100d:+0.##;-0.##;0}%)";
        }

        // onlyChanged: yalnız değişen satırlar. İki görüntü de sayı / metin kopyasıdır; karşılaştırma oyuna dokunmaz.
        public static List<DiffRow> Compare(NumericSnapshot before, NumericSnapshot after, bool onlyChanged)
        {
            var result = new List<DiffRow>();
            if (before == null || after == null) return result;
            var old = new Dictionary<string, SnapshotRow>();
            foreach (SnapshotRow row in before.Rows) old[row.Key] = row;
            foreach (SnapshotRow row in after.Rows)
            {
                old.TryGetValue(row.Key, out SnapshotRow previous);
                old.Remove(row.Key);
                var diff = new DiffRow { Label = row.Label, Scope = row.Scope, Unit = row.HasValue ? UnitName(row.Unit) : "durum", Note = Join(row.Context, row.Limit) };
                if (previous == null)
                {
                    diff.Before = "(yoktu)"; diff.After = row.HasValue ? Format(row.Unit, row.Value) : row.Text; diff.Changed = true;
                }
                else if (!row.HasValue || !previous.HasValue)
                {
                    diff.Before = previous.HasValue ? Format(previous.Unit, previous.Value) : previous.Text;
                    diff.After = row.HasValue ? Format(row.Unit, row.Value) : row.Text;
                    diff.Changed = diff.Before != diff.After;
                    diff.Delta = diff.Changed ? "durum değişti" : "";
                }
                else
                {
                    diff.Before = Format(row.Unit, previous.Value); diff.After = Format(row.Unit, row.Value);
                    diff.Changed = System.Math.Abs(row.Value - previous.Value) > 1e-7 * System.Math.Max(1d, System.Math.Abs(previous.Value));
                    if (diff.Changed) { diff.Delta = FormatDelta(row.Unit, previous.Value, row.Value); diff.Ratio = FormatRatio(row.Unit, previous.Value, row.Value); }
                }
                diff.Source = SourceChange(previous, row);
                if (diff.Source.Length > 0) diff.Changed = true;
                if (diff.Changed || !onlyChanged) result.Add(diff);
            }
            foreach (SnapshotRow gone in old.Values)
                result.Add(new DiffRow { Label = gone.Label, Scope = gone.Scope, Before = gone.HasValue ? Format(gone.Unit, gone.Value) : gone.Text, After = "(artık yok)", Changed = true, Unit = gone.HasValue ? UnitName(gone.Unit) : "durum" });
            return result;
        }

        private static string Join(string a, string b) => string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + " · " + b;

        // Kaynak listesindeki fark (çoklu küme): eklenenler "+", kalkanlar "−".
        private static string SourceChange(SnapshotRow before, SnapshotRow after)
        {
            var removed = new List<string>(before != null ? before.Sources : new List<string>());
            var text = new StringBuilder();
            foreach (string source in after.Sources)
            {
                int index = removed.IndexOf(source);
                if (index >= 0) removed.RemoveAt(index);
                else text.Append(text.Length > 0 ? "; " : "").Append("+ ").Append(source);
            }
            foreach (string source in removed) text.Append(text.Length > 0 ? "; " : "").Append("− ").Append(source);
            return text.ToString();
        }

        public static string ToText(NumericSnapshot before, NumericSnapshot after, List<DiffRow> rows)
        {
            var text = new StringBuilder();
            text.Append("ÖNCE / SONRA · ").Append(after.Target).Append('\n');
            text.Append($"önce: {before.Title} (R{before.Round}, {before.GameTime:0.00} sn) → sonra: {after.Title} (R{after.Round}, {after.GameTime:0.00} sn)\n");
            foreach (DiffRow row in rows)
            {
                text.Append(row.Label).Append(" [").Append(row.Scope).Append("] | Önce: ").Append(row.Before).Append(" | Sonra: ").Append(row.After);
                if (row.Delta.Length > 0) text.Append(" | Fark: ").Append(row.Delta);
                if (row.Ratio.Length > 0) text.Append(" | Oran: ").Append(row.Ratio);
                if (row.Source.Length > 0) text.Append(" | Kaynak: ").Append(row.Source);
                if (row.Note.Length > 0) text.Append(" | ").Append(row.Note);
                text.Append('\n');
            }
            if (rows.Count == 0) text.Append("Değişen satır yok.\n");
            return text.ToString();
        }

        public static string ToCsv(List<DiffRow> rows)
        {
            var text = new StringBuilder("stat;kapsam;birim;once;sonra;fark;oran;kaynak;not\n");
            foreach (DiffRow row in rows)
                text.Append(Csv(row.Label)).Append(';').Append(Csv(row.Scope)).Append(';').Append(Csv(row.Unit)).Append(';').Append(Csv(row.Before)).Append(';').Append(Csv(row.After)).Append(';')
                    .Append(Csv(row.Delta)).Append(';').Append(Csv(row.Ratio)).Append(';').Append(Csv(row.Source)).Append(';').Append(Csv(row.Note)).Append('\n');
            return text.ToString();
        }

        public static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"").Replace("\n", " ") + "\"";
    }
}
