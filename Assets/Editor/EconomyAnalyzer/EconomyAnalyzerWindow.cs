using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ClickerGame.EconomyAnalysis
{
    public sealed class EconomyAnalyzerWindow : EditorWindow
    {
        EconomyBalanceProfileSO working, source, runProfile;
        UnityEditor.Editor profileEditor;
        List<EconomySnapshot> without, with;
        EconomySimulationResult resultOff, resultOn;
        Vector2 scroll;
        bool settings = true, running;
        int completed, displayRound = 129;
        string error, laneSource;
        SkillNodeSO pricedSkill;
        int pricedTier = 1;
        ResourceType pricingCurrency = ResourceType.Gold;
        PlanterSO comparePlanter;
        EconomySnapshot comparison;

        [MenuItem("Tools/Economy Analyzer")]
        public static void Open()
        {
            var window = GetWindow<EconomyAnalyzerWindow>("Economy Analyzer");
            window.minSize = new Vector2(1050, 650);
        }
        void OnEnable() { if (working == null) working = EconomyEditorData.Defaults(); }
        void OnDisable()
        {
            Stop();
            if (profileEditor != null) DestroyImmediate(profileEditor);
            if (working != null) DestroyImmediate(working);
            if (runProfile != null) DestroyImmediate(runProfile);
        }
        void Stop() { running = false; EditorApplication.update -= Tick; }
        void Tick()
        {
            try
            {
                // One paired trial per editor tick keeps cancellation and repaint available.
                resultOff.Trials.Add(EconomySimulation.RunTrial(without, runProfile.currentXpCurve, runProfile.simulationFps, runProfile.seed, completed));
                resultOn.Trials.Add(EconomySimulation.RunTrial(with, runProfile.currentXpCurve, runProfile.simulationFps, runProfile.seed, completed));
                completed++;
                if (completed >= runProfile.trials) Stop();
            }
            catch (Exception ex) { error = ex.Message; Stop(); }
            Repaint();
        }
        void StartAnalysis()
        {
            Stop(); error = null; comparison = null;
            if (runProfile != null) DestroyImmediate(runProfile);
            runProfile = Instantiate(working); runProfile.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                if (runProfile.trials < 1 || runProfile.trials > 1000) throw new ArgumentException("Trials must be 1–1000.");
                EconomyXp.Needed(1, runProfile.currentXpCurve, false);
                EconomyEditorData.Spawners(runProfile, out laneSource);
                without = EconomyEditorData.History(runProfile, false);
                with = EconomyEditorData.History(runProfile, true);
                resultOff = new EconomySimulationResult(); resultOn = new EconomySimulationResult(); completed = 0;
                displayRound = Mathf.Clamp(runProfile.checkpoint, 1, 130);
                running = true; EditorApplication.update += Tick;
            }
            catch (Exception ex) { error = ex.Message; without = with = null; }
        }
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Economy Analyzer — analiz ayarları gameplay'i değiştirmez", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Tek saksı modeli. Simülasyon gerçek üretim/ölüm döngüsünü kullanır; hedef seçimi, sabit FPS ve frame sırası varsayımdır. " +
                "EffectiveTargets bir gameplay limiti değildir. Patlama, tornado, diğer saksılar, kart skip geliri ve otomatik alışveriş bu modelde simüle edilmez.", MessageType.Info);
            using (new EditorGUI.DisabledScope(running))
            {
                var selected = (EconomyBalanceProfileSO)EditorGUILayout.ObjectField("Profil yükle (oturum kopyası)", source, typeof(EconomyBalanceProfileSO), false);
                if (selected != source)
                {
                    source = selected;
                    if (profileEditor != null) DestroyImmediate(profileEditor);
                    if (working != null) DestroyImmediate(working);
                    working = selected != null ? Instantiate(selected) : EconomyEditorData.Defaults();
                    working.hideFlags = HideFlags.HideAndDontSave;
                }
                settings = EditorGUILayout.Foldout(settings, "Girdiler / build / sabit tile roll'leri / round geçmişi", true);
                if (settings)
                {
                    if (profileEditor == null) profileEditor = UnityEditor.Editor.CreateEditor(working);
                    profileEditor.OnInspectorGUI();
                    EditorGUILayout.HelpBox("History satırları açık varsayımlardır; bir sonraki satıra kadar aynı build kullanılır. " +
                        "Tile rolledModifiers listesi gerçek roll değerleridir; boş liste sayısal tile bonusu vermez. " +
                        "BASE / MAX karşılaştırması için History build alanını seç. MAX roster varsayılan olarak FinalSkillTree asset klasöründen okunur.", MessageType.None);
                }
                if (GUILayout.Button("Analytical + Seeded Monte Carlo + Resonance A/B çalıştır")) StartAnalysis();
                if (GUILayout.Button("Analiz profilini yeni dosya olarak kaydet"))
                {
                    string path = EditorUtility.SaveFilePanelInProject("Save analysis profile", "EconomyAnalysisProfile", "asset", "Only analysis settings are saved.");
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (AssetDatabase.LoadMainAssetAtPath(path) != null) error = "Mevcut dosyanın üzerine yazılmaz; yeni ad seç.";
                        else { var copy = Instantiate(working); copy.hideFlags = HideFlags.None; AssetDatabase.CreateAsset(copy, path); }
                    }
                }
            }
            if (running && GUILayout.Button($"İptal — {completed}/{runProfile.trials} eşlenmiş deneme")) Stop();
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (without != null && with != null)
            {
                EditorGUILayout.Space();
                displayRound = EditorGUILayout.IntSlider("Gösterilen round", displayRound, 1, 130);
                EditorGUILayout.LabelField($"Son çalıştırma: seed {runProfile.seed}, {runProfile.simulationFps} FPS, {completed} deneme. Sonuçlar bu girdilerin anlık görüntüsüdür.");
                DrawResults();
            }
            EditorGUILayout.EndScrollView();
        }

        static string F(double value) => double.IsNaN(value) ? "N/A" : double.IsPositiveInfinity(value) ? "∞ / unreachable" : value.ToString("N2");
        static void Row(string name, params string[] values)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(name, GUILayout.MinWidth(190));
                foreach (string value in values) EditorGUILayout.LabelField(value, GUILayout.MinWidth(85));
            }
        }
        void DrawResults()
        {
            int index = displayRound - 1;
            var snap = runProfile.enableResonance ? with[index] : without[index];
            var simulation = runProfile.enableResonance ? resultOn : resultOff;
            var input = snap.Input;
            EditorGUILayout.LabelField($"{input.Planter.planterName} — {input.SpawnerCount} lanes — {laneSource}", EditorStyles.boldLabel);
            Row("Üretim / saldırı aralığı", F(input.SpawnInterval) + " s", F(input.AttackInterval) + " s");
            Row("Round / EffectiveTargets", F(input.Duration) + " s", input.EffectiveTargets.ToString());
            if (input.ExplosionChance > 0 || input.TornadoChance > 0 || input.BoomerangChance > 0 || input.ElectricChance > 0)
                EditorGUILayout.HelpBox("Bu build patlama/tornado/orak/elektrik içeriyor. Aşağıdaki simülasyon direct-hit + Duplicate kapsamındadır; tam build sonucu değildir.", MessageType.Warning);
            Row("Instant-Harvest Production Ceiling", F(snap.ProductionCeiling));
            Row("Analytical Combat Capacity", F(snap.CombatCapacity));
            Row("Combat / Spawn", F(snap.CombatToSpawnRatio), snap.Bottleneck);
            Row("Analytical utilization", F(snap.Utilization * 100) + "%");
            EditorGUILayout.Space();
            Row("Ölçüm", "Analytical V0 Estimate", "Simulated Average", "Median/P50", "P10", "P90");
            Metric("Gold / round", snap.Estimate.Gold, simulation, index, r => r.Income.Gold);
            Metric("Iron / round", snap.Estimate.Iron, simulation, index, r => r.Income.Iron);
            Metric("Stone / round", snap.Estimate.Stone, simulation, index, r => r.Income.Stone);
            Metric("XP / round", snap.Estimate.Xp, simulation, index, r => r.Income.Xp);
            Metric("Harvest Score / round", snap.Estimate.Score, simulation, index, r => r.Income.Score);
            Metric("Harvest count / round", snap.EstimatedHarvests, simulation, index, r => r.Income.Harvests);
            var income = completed > 0 ? simulation.Average(index) : snap.Estimate;
            Row("Gelir / saniye (G / I / S / XP)", F(income.Gold / input.Duration), F(income.Iron / input.Duration), F(income.Stone / input.Duration), F(income.Xp / input.Duration));
            Row("Weighted income (analysis-only)", F(income.Weighted(input)));
            Row("EV / spawn (G / I / S / XP)", F(snap.PerSpawn.Gold), F(snap.PerSpawn.Iron), F(snap.PerSpawn.Stone), F(snap.PerSpawn.Xp));
            EditorGUILayout.LabelField("Normalize bitki olasılıkları", EditorStyles.boldLabel);
            foreach (var plant in snap.Plants) Row(plant.Plant.plantName, F(plant.Probability * 100) + "%", "HP " + plant.Hp,
                plant.RoundedReward + " " + plant.Plant.resourceType, plant.RoundedXp + " XP");
            DrawPurchase(input, income);
            DrawXp(simulation, index, income);
            DrawResonance(index);
            DrawBuildComparison(input);
            DrawCheckpoints(simulation);
        }
        void Metric(string name, double analytical, EconomySimulationResult sim, int index, Func<EconomyTrialRound, double> get)
        {
            if (completed == 0) { Row(name, F(analytical), "Pending", "—", "—", "—"); return; }
            var d = sim.Distribution(index, get);
            Row(name, F(analytical), F(d.Average), F(d.P50), F(d.P10), F(d.P90));
        }
        void DrawPurchase(EconomyInput input, EconomyAmounts income)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Satın alma / fiyat aralığı", EditorStyles.boldLabel);
            var cost = new EconomyAmounts(); cost.AddCurrency(input.Planter.costType, input.Planter.cost);
            var wait = EconomyCalculator.PurchaseWait(cost, income);
            Row("Saksı maliyeti / bekleme", input.Planter.cost + " " + input.Planter.costType, F(wait.rounds) + " round", wait.currency);
            pricedSkill = (SkillNodeSO)EditorGUILayout.ObjectField("Fiyatı incelenecek skill", pricedSkill, typeof(SkillNodeSO), false);
            if (pricedSkill != null && pricedSkill.tiers.Count > 0)
            {
                pricedTier = EditorGUILayout.IntSlider("Satın alınacak tier", pricedTier, 1, pricedSkill.tiers.Count);
                var tier = pricedSkill.tiers[pricedTier - 1]; var skillCost = new EconomyAmounts(); skillCost.AddCurrency(tier.costType, tier.cost);
                var skillWait = EconomyCalculator.PurchaseWait(skillCost, income);
                Row("Skill maliyeti / bekleme", tier.cost + " " + tier.costType, F(skillWait.rounds) + " round", skillWait.currency);
                var owned = CurrentRow();
                EconomyRoundAssumption TierRow(int level)
                {
                    var copy = new EconomyRoundAssumption { build = EconomyBuild.Custom, durationOverride = owned.durationOverride,
                        effectiveTargets = owned.effectiveTargets, extraGlobalModifiers = owned.extraGlobalModifiers, tiles = owned.tiles };
                    if (owned.build == EconomyBuild.MaxSkillTree)
                        copy.purchases.AddRange(runProfile.skillTree.Where(n => n != null && n != pricedSkill).Distinct()
                            .Select(n => new SkillPurchase { node = n, level = n.tiers.Count }));
                    else if (owned.build == EconomyBuild.Custom)
                        copy.purchases.AddRange(owned.purchases.Where(p => p.node != pricedSkill));
                    copy.purchases.Add(new SkillPurchase { node = pricedSkill, level = level });
                    return copy;
                }
                var before = EconomyCalculator.Analyze(EconomyCalculator.Resolve(runProfile, TierRow(pricedTier - 1), displayRound, input.SpawnerCount, runProfile.enableResonance));
                var after = EconomyCalculator.Analyze(EconomyCalculator.Resolve(runProfile, TierRow(pricedTier), displayRound, input.SpawnerCount, runProfile.enableResonance));
                double beforeValue = before.Estimate.Weighted(input), afterValue = after.Estimate.Weighted(input);
                Row("Analytical tier " + (pricedTier - 1) + " → " + pricedTier, F(beforeValue), F(afterValue), "Δ " + F(afterValue - beforeValue));
                Row("Artış / skill ROI", F((EconomyCalculator.Ratio(afterValue, beforeValue) - 1) * 100) + "%",
                    F(EconomyCalculator.Roi(skillCost, before.Estimate, after.Estimate, input)) + " rounds");
                EditorGUILayout.LabelField("Yalnız seçili node değiştirilir. Grid/unlock'ın gelecekteki saksı yatırımı otomatik eklenmez; ROI bu stratejik değeri içermez.", EditorStyles.wordWrappedMiniLabel);
            }
            pricingCurrency = (ResourceType)EditorGUILayout.EnumPopup("Öneri para birimi", pricingCurrency);
            foreach (var category in new[] { ("Minor stat", .5, 1d), ("Normal stat", 1d, 2d), ("Strong stat", 2d, 3d),
                ("Keystone", 3d, 5d), ("System / planter unlock", 1d, 2.5) })
                Row(category.Item1, F(income.Currency(pricingCurrency) * category.Item2) + " – " + F(income.Currency(pricingCurrency) * category.Item3),
                    wait.rounds < category.Item2 ? "Purchasing very fast" : wait.rounds > category.Item3 ? "Purchasing too slow" : "Purchasing in target range");
            EditorGUILayout.LabelField("Durum etiketleri seçili saksı maliyetini her kategoriyle kıyaslar; önkoşul maliyetleri ve mevcut bakiye dahil değildir.", EditorStyles.wordWrappedMiniLabel);
        }
        void DrawXp(EconomySimulationResult simulation, int index, EconomyAmounts income)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("XP — birikimli round geçmişi", EditorStyles.boldLabel);
            if (completed == 0) return;
            double total = simulation.Distribution(index, r => r.CumulativeXp).Average;
            Row("Ortalama toplam XP", F(total));
            foreach (bool proposed in new[] { false, true })
            {
                string label = proposed ? "Proposed XP curve" : "Current XP curve";
                var level = simulation.Distribution(index, r => proposed ? r.ProposedLevel : r.CurrentLevel);
                var state = EconomyXp.State(total, runProfile.currentXpCurve, proposed);
                Row(label + " — level Avg/P50/P10/P90", F(level.Average), F(level.P50), F(level.P10), F(level.P90));
                Row("Mean XP temsilcisi: next / kalan", F(state.needed), F(state.needed - state.progress),
                    F(income.Xp > 0 ? (state.needed - state.progress) / income.Xp : double.PositiveInfinity) + " round (current-rate estimate)");
                Row("Bu level için kümülatif eşik", F(EconomyXp.CumulativeTo(state.level, runProfile.currentXpCurve, proposed)));
                var target = EconomyXp.Target(displayRound);
                if (target.low > 0) Row("Hedef / sapma", target.low + "–" + target.high,
                    level.Average < target.low ? F(level.Average - target.low) + " XP behind target" :
                    level.Average > target.high ? "+" + F(level.Average - target.high) + " XP ahead of target" : "XP near target");
                int next = new[] { 20, 40, 65, 100, 120 }.FirstOrDefault(r => r > displayRound);
                if (next > 0)
                {
                    var nextTarget = EconomyXp.Target(next);
                    double required = Math.Max(0, EconomyXp.CumulativeTo(nextTarget.low, runProfile.currentXpCurve, proposed) - total) / (next - displayRound);
                    Row("R" + next + " hedef alt sınırı: XP/round", F(required),
                        "Suggested XP production ×" + F(required == 0 ? 0 : income.Xp > 0 ? required / income.Xp : double.PositiveInfinity));
                }
            }
            EditorGUILayout.HelpBox("121 sonrası stat kartı sistemi mevcut kodda yok. Proposed curve yalnız analiz karşılaştırmasıdır; build/kart etkileri otomatik uygulanmaz.", MessageType.None);
        }
        void DrawResonance(int index)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Rezonans A/B — aynı build, tile layout, roll'ler ve seed", EditorStyles.boldLabel);
            if (completed == 0) return;
            var a = resultOff.Average(index); var b = resultOn.Average(index);
            Row("Ölçüm", "Resonance OFF", "Resonance ON", "ON / OFF");
            Row("Gold", F(a.Gold), F(b.Gold), F(EconomyCalculator.Ratio(b.Gold, a.Gold)));
            Row("Iron", F(a.Iron), F(b.Iron), F(EconomyCalculator.Ratio(b.Iron, a.Iron)));
            Row("Stone", F(a.Stone), F(b.Stone), F(EconomyCalculator.Ratio(b.Stone, a.Stone)));
            Row("XP", F(a.Xp), F(b.Xp), F(EconomyCalculator.Ratio(b.Xp, a.Xp)));
            Row("Weighted income", F(a.Weighted(with[index].Input)), F(b.Weighted(with[index].Input)), F(EconomyCalculator.Ratio(b.Weighted(with[index].Input), a.Weighted(with[index].Input))));
            Row("Instant-Harvest Production Ceiling", F(without[index].ProductionCeiling), F(with[index].ProductionCeiling), F(EconomyCalculator.Ratio(with[index].ProductionCeiling, without[index].ProductionCeiling)));
            Row("Analytical Combat Capacity", F(without[index].CombatCapacity), F(with[index].CombatCapacity), F(EconomyCalculator.Ratio(with[index].CombatCapacity, without[index].CombatCapacity)));
            if (EconomyCalculator.Ratio(b.Weighted(with[index].Input), a.Weighted(with[index].Input)) >= 2 || EconomyCalculator.Ratio(b.Xp, a.Xp) >= 2)
                EditorGUILayout.HelpBox("≥2× birleşik artış: tasarımcı incelemesi için işaretlendi. Bu bir nerf önerisi veya otomatik denge kararı değildir.", MessageType.Info);
            var row = CurrentRow();
            var families = row.tiles.Select(t => t.tile.modifierType).Distinct().ToArray();
            double independentEconomy = 1, independentXp = 1;
            double baseValue = without[index].Estimate.Weighted(without[index].Input);
            EditorGUILayout.LabelField("Aile katkıları — Analytical V0 (aynı tile roll'leri)", EditorStyles.boldLabel);
            foreach (var family in families)
            {
                var single = EconomyCalculator.Analyze(EconomyCalculator.Resolve(runProfile, row, displayRound, with[index].Input.SpawnerCount, true,
                    new HashSet<TileModifierType> { family }));
                double economyRatio = EconomyCalculator.Ratio(single.Estimate.Weighted(single.Input), baseValue);
                double xpRatio = EconomyCalculator.Ratio(single.Estimate.Xp, without[index].Estimate.Xp);
                independentEconomy *= economyRatio; independentXp *= xpRatio;
                Row(family.ToString(), "Economy ×" + F(economyRatio), "XP ×" + F(xpRatio),
                    "Spawn ×" + F(EconomyCalculator.Ratio(single.ProductionCeiling, without[index].ProductionCeiling)),
                    "Combat ×" + F(EconomyCalculator.Ratio(single.CombatCapacity, without[index].CombatCapacity)));
            }
            double combined = EconomyCalculator.Ratio(with[index].Estimate.Weighted(with[index].Input), baseValue);
            double combinedXp = EconomyCalculator.Ratio(with[index].Estimate.Xp, without[index].Estimate.Xp);
            if (families.Length > 1 && (combined > independentEconomy * 1.25 || combinedXp > independentXp * 1.25))
                EditorGUILayout.HelpBox("Güçlü etkileşim: birleşik analitik kazanç, tekil aile çarpanlarının çarpımını %25'ten fazla aşıyor. " +
                    "Bu eşik yalnız tasarımcı inceleme işaretidir; Monte Carlo A/B sonuçlarıyla birlikte değerlendir.", MessageType.Info);
        }
        EconomyRoundAssumption CurrentRow() => runProfile.history.Where(r => r.fromRound <= displayRound).OrderBy(r => r.fromRound).Last();
        void DrawBuildComparison(EconomyInput input)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("BASE → MAX SKILL TREE (tile/resonance hariç; aynı hedef varsayımı)", EditorStyles.boldLabel);
            var baseRow = new EconomyRoundAssumption { effectiveTargets = input.EffectiveTargets };
            var maxRow = new EconomyRoundAssumption { build = EconomyBuild.MaxSkillTree, effectiveTargets = input.EffectiveTargets };
            var a = EconomyCalculator.Resolve(runProfile, baseRow, displayRound, input.SpawnerCount, false);
            var b = EconomyCalculator.Resolve(runProfile, maxRow, displayRound, input.SpawnerCount, false);
            void Stat(string name, double av, double bv) => Row(name, F(av), F(bv), "×" + F(EconomyCalculator.Ratio(bv, av)));
            Stat("Damage", a.Damage, b.Damage); Stat("Attack interval (lower=faster)", a.AttackInterval, b.AttackInterval);
            Stat("Area radius", a.Radius, b.Radius); Stat("Crit chance", a.CritChance, b.CritChance);
            Stat("Crit multiplier", a.CritMultiplier, b.CritMultiplier); Stat("Spawn interval (lower=faster)", a.SpawnInterval, b.SpawnInterval);
            Stat("Rare bonus", a.RareBonus, b.RareBonus); Stat("Gold multiplier", a.GoldMultiplier, b.GoldMultiplier);
            Stat("Iron multiplier", a.IronMultiplier, b.IronMultiplier); Stat("Stone multiplier", a.StoneMultiplier, b.StoneMultiplier);
            Stat("XP multiplier", a.XpMultiplier, b.XpMultiplier); Stat("Duration", a.Duration, b.Duration);
            var before = EconomyCalculator.Analyze(a).Estimate; var after = EconomyCalculator.Analyze(b).Estimate;
            double delta = after.Weighted(b) - before.Weighted(a);
            var cost = new EconomyAmounts();
            foreach (var node in runProfile.skillTree.Where(n => n != null).Distinct())
                foreach (var tier in node.tiers) cost.AddCurrency(tier.costType, tier.cost);
            Stat("Analytical weighted income / round", before.Weighted(a), after.Weighted(b));
            Row("Delta / increase / all-tree ROI", F(delta), F((EconomyCalculator.Ratio(after.Weighted(b), before.Weighted(a)) - 1) * 100) + "%",
                F(EconomyCalculator.Roi(cost, before, after, b)) + " rounds");
            EditorGUILayout.LabelField("ROI tüm ağacın maliyetini kullanır; stratejik değer puanı değildir. Per-round karşılaştırması süre artışını da içerir.", EditorStyles.wordWrappedMiniLabel);
            comparePlanter = (PlanterSO)EditorGUILayout.ObjectField("Ek saksı karşılaştırması", comparePlanter, typeof(PlanterSO), false);
            if (comparePlanter != null && GUILayout.Button("BASE saksılarını analitik karşılaştır"))
            {
                var copy = Instantiate(runProfile);
                try
                {
                    copy.planter = comparePlanter; copy.spawnerCountOverride = 0;
                    comparison = EconomyCalculator.Analyze(EconomyCalculator.Resolve(copy, baseRow, displayRound, EconomyEditorData.Spawners(copy, out _), false));
                }
                catch (Exception ex) { error = ex.Message; }
                finally { DestroyImmediate(copy); }
            }
            if (comparison != null) Row(comparison.Input.Planter.planterName + " (BASE snapshot R" + comparison.Input.Round + ")",
                F(comparison.Estimate.Gold) + " G", F(comparison.Estimate.Iron) + " I", F(comparison.Estimate.Stone) + " S", F(comparison.Estimate.Xp) + " XP");
        }
        void DrawCheckpoints(EconomySimulationResult simulation)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Checkpoint özeti — explicit history", EditorStyles.boldLabel);
            foreach (int round in runProfile.checkpoints.Where(r => r >= 1 && r <= 130).Distinct().OrderBy(r => r))
            {
                var s = runProfile.enableResonance ? with[round - 1] : without[round - 1];
                var target = EconomyXp.Target(round);
                string xp = "Pending";
                if (completed > 0)
                {
                    double level = simulation.Distribution(round - 1, t => t.CurrentLevel).Average;
                    xp = "Lv " + F(level) + (target.low == 0 ? " (no target)" : level < target.low ? " behind target" : level > target.high ? " ahead of target" : " near target");
                }
                Row("R" + round, s.Bottleneck, xp);
            }
        }
    }
}
