using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MechanicsTestLab
{
    // D · Boss ödülleri ve kartlar. İki yol ayrı gösterilir: A) normal kural testi (oyunun teklif ekranı), B) kontrollü kurulum.
    public sealed partial class LabWindow
    {
        private readonly List<BossRewardSO> rewardList = new();
        private readonly List<BossRewardSO> conflictList = new();
        private readonly List<int> placementList = new();
        private BossRewardSO selectedReward;
        private string rewardSearch = "";
        private int offerRound;
        private bool offerRealTargets;
        private int placementIndex;

        private void DrawRewards()
        {
            BossRewardManager boss = TestRewards.Boss;
            RoundManager rounds = RoundManager.Instance;
            if (boss == null || rounds == null) { EditorGUILayout.LabelField("Ödül yöneticisi yok."); return; }
            if (boss.Pool == null) { EditorGUILayout.HelpBox("Bu profilin boss ödül havuzu yok.", MessageType.Info); return; }

            Header("A) NORMAL KURAL TESTİ — oyunun gerçek teklif ekranı");
            if (offerRound <= 0 || !rounds.IsBossRound(offerRound)) offerRound = Mathf.Max(1, TestRewards.NextBossRound(rounds, rounds.CurrentRound));
            EditorGUILayout.BeginHorizontal();
            offerRound = Mathf.Clamp(EditorGUILayout.IntField("Boss round'u", offerRound), 1, rounds.MaxRounds);
            if (GUILayout.Button("Sonraki boss", GUILayout.Width(100f)))
            {
                int next = TestRewards.NextBossRound(rounds, offerRound + 1);
                offerRound = next > 0 ? next : TestRewards.NextBossRound(rounds, 1);
            }
            EditorGUILayout.EndHorizontal();
            BossRewardStage stage = boss.Pool.IsStaged ? boss.Pool.StageAt(offerRound) : null;
            EditorGUILayout.LabelField($"R{offerRound}: {(rounds.IsBossRound(offerRound) ? "boss round'u" : "BOSS ROUND'U DEĞİL")}" + (stage != null ? $" · teklif aşaması {stage.displayName}" : "") +
                                       $" · teklif seed'i {TestSession.Profile.bossSeed}", EditorStyles.miniLabel);
            offerRealTargets = EditorGUILayout.ToggleLeft("Kota ve boss hasadını gerçekten değerlendir (tutmazsa ödül yok, run biter)", offerRealTargets);
            using (new EditorGUI.DisabledScope(boss.IsPending))
                if (GUILayout.Button("Boss round'unu oyunun akışıyla bitir ve gerçek teklifi aç", GUILayout.Height(24f))) Say(TestRewards.OpenNormalOffer(offerRound, !offerRealTargets));
            EditorGUILayout.LabelField("Teklif oyunun üreticisinden çıkar (aşama, uygunluk, stack, dışlama, seed) ve ödül oyunun ekranından alınır. " +
                                       "Seçimden sonra oyunun round özeti gelir; 'Sonraki round' ile ya da 'A · Oturum > Round'a git' ile devam et.", EditorStyles.wordWrappedMiniLabel);
            if (boss.IsPending) EditorGUILayout.HelpBox("Bekleyen bir boss teklifi var: seçimi Game görünümünde yap. Teklif açıkken kontrollü kurulum yapılmaz.", MessageType.Info);

            Header("Level kartları (oyunun kart ekranı)");
            EditorGUILayout.LabelField($"Bekleyen seçim: {rounds.PendingCardSelections} · verilen hak {rounds.CardChoicesGranted} · alınan kart {rounds.CardsTaken} · level başına hak {rounds.ChoicesPerLevel}");
            ProgressionManager progression = ProgressionManager.Instance;
            if (progression != null)
            {
                bool hasEmpty = progression.HasEligibleCell();
                // Sayım burada yapılır: oyunun aday listesi (GetUpgradeCandidates) karıştırırken oyunun rastgelelik akışını tüketir.
                int upgradable = 0;
                GridManager grid = GridManager.Instance;
                for (int x = 0; x < grid.GetWidth(); x++)
                    for (int z = 0; z < grid.GetHeight(); z++)
                    {
                        GroundCell ground = TestField.Ground(new GridPosition(x, z));
                        if (ground != null && ground.CanUpgrade) upgradable++;
                    }
                EditorGUILayout.LabelField("Sıradaki kart türü: " + (hasEmpty ? "YENİ TILE (açık boş hücre var; yeri rastgele seçilir)" : upgradable > 0
                    ? $"TILE YÜKSELTMESİ (grid dolu; {upgradable} tile yükseltilebilir)" : "TEMEL GÜÇ KARTI (bütün tile'lar azami seviyede)"), EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Açık boş hücreleri tile ile doldur (→ yükseltme kartları)")) Say(FillEmptyCells(0));
            if (GUILayout.Button("Bütün tile'ları azami seviyeye çıkar (→ temel güç kartı)")) Say(MaxAllTiles());
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Kart ekranını açmak için: 'C · Statlar'dan XP ver (seçim hakkı doğar), sonra 'A · Oturum > Round'u şimdi bitir'. Kart oyunun kendi " +
                                       "uygulama yolundan geçer. XP'yi hedef round'a geldikten SONRA ver: 'Round'a git' ve 'Round'u yeniden başlat' bekleyen seçimleri sıfırlar " +
                                       "(oyunun round atlama girişi). Doldurma, 'B · Tarla'daki tile türünü ve nadirliğini kullanır.", EditorStyles.wordWrappedMiniLabel);

            Header("B) KONTROLLÜ KURULUM — teklif ve round beklenmeden");
            if (rewardList.Count == 0) TestRewards.Catalog(rewardList);
            rewardSearch = EditorGUILayout.TextField("Ödül ara", rewardSearch);
            EditorGUILayout.LabelField($"{rewardList.Count} ödül · havuz {boss.Pool.name} (gerçek veriden)", EditorStyles.miniLabel);
            foreach (BossRewardSO reward in rewardList)
            {
                if (!string.IsNullOrWhiteSpace(rewardSearch) &&
                    (reward.displayName ?? "").IndexOf(rewardSearch.Trim(), System.StringComparison.CurrentCultureIgnoreCase) < 0 &&
                    (reward.id ?? "").IndexOf(rewardSearch.Trim(), System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                BossRewardBlock block = boss.BlockOf(reward, true);
                BossRewardStage rewardStage = boss.StageOf(reward);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Toggle(selectedReward == reward, reward.displayName, EditorStyles.miniButton, GUILayout.Width(170f)) && selectedReward != reward) { selectedReward = reward; placementIndex = 0; }
                GUILayout.Label(rewardStage != null ? rewardStage.displayName : "—", EditorStyles.miniLabel, GUILayout.Width(52f));
                GUILayout.Label($"{boss.Stacks(reward)}/{reward.maxStacks}", EditorStyles.miniLabel, GUILayout.Width(30f));
                GUILayout.Label(TestRewards.BlockText(reward, block), block == BossRewardBlock.None ? EditorStyles.miniLabel : EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUI.DisabledScope(block != BossRewardBlock.None || boss.IsPending))
                    if (GUILayout.Button("Kur", GUILayout.Width(44f))) { selectedReward = reward; Say(TestRewards.Grant(reward, ChosenPlacement(reward))); }
                EditorGUILayout.EndHorizontal();
            }

            if (selectedReward == null) return;
            Header("Seçili ödül");
            EditorGUILayout.HelpBox(TestRewards.Describe(selectedReward), MessageType.None);
            BossRewardBlock selectedBlock = boss.BlockOf(selectedReward, true);
            if (boss.NeedsPlacement(selectedReward))
            {
                TestRewards.Placements(selectedReward, placementList);
                if (placementList.Count == 0) EditorGUILayout.LabelField("Geçerli yerleşim konumu yok.");
                else
                {
                    var names = new string[placementList.Count];
                    for (int i = 0; i < names.Length; i++) names[i] = AntennaSlots.Name(placementList[i]);
                    placementIndex = Mathf.Clamp(EditorGUILayout.Popup("Yerleşim konumu", placementIndex, names), 0, names.Length - 1);
                }
            }
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(boss.IsPending))
            {
                if (selectedBlock == BossRewardBlock.Prerequisite && GUILayout.Button("Ön koşullarıyla birlikte kur"))
                {
                    var report = new List<Result>();
                    Result result = TestRewards.GrantWithPrerequisites(selectedReward, report);
                    Say(result.Ok ? Result.Success($"{report.Count} ödül kuruldu (ön koşullar dahil): {selectedReward.displayName}.") : result);
                }
                if (selectedBlock == BossRewardBlock.Exclusive && GUILayout.Button("Çakışanı kaldırıp yeniden kur (temiz oturum)")) ReinstallWithoutConflicts(selectedReward);
            }
            EditorGUILayout.EndHorizontal();
            if (selectedBlock == BossRewardBlock.Exclusive)
                EditorGUILayout.LabelField("Oyunda ödül geri alma yoktur: bu işlem mevcut düzeni yakalar, çakışan ödülü listeden çıkarır, bu ödülü ekler ve senaryoyu temiz oturumdan " +
                                           "yeniden kurar. Dışlama kuralı aşılmaz.", EditorStyles.wordWrappedMiniLabel);
        }

        private int ChosenPlacement(BossRewardSO reward)
        {
            if (reward != selectedReward || TestRewards.Boss == null || !TestRewards.Boss.NeedsPlacement(reward)) return RunEffectSO.NoPlacement;
            TestRewards.Placements(reward, placementList);
            return placementList.Count > 0 ? placementList[Mathf.Clamp(placementIndex, 0, placementList.Count - 1)] : RunEffectSO.NoPlacement;
        }

        private void ReinstallWithoutConflicts(BossRewardSO reward)
        {
            TestRewards.Conflicts(reward, conflictList);
            Scenario scenario = ScenarioRunner.Capture(TestSession.Current);
            foreach (BossRewardSO conflict in conflictList) scenario.rewards.RemoveAll(entry => entry.id == conflict.id);
            scenario.rewards.Add(new ScenarioReward { id = reward.id, stacks = 1 });
            scenario.name = TestSession.Current.name + " · " + reward.displayName;
            Say(TestSession.Rebuild(scenario));
        }

        // Kart türünü denemek için: açık boş hücrelere 'B · Tarla'da seçili tile konur (grid dolunca kartlar yükseltme verir).
        private Result FillEmptyCells(int level)
        {
            TileModifierSO tile = LabCatalog.Tile(tileType, tileRarity);
            if (tile == null) return Result.Fail("'B · Tarla'da seçili tür / nadirlik için tile yok.");
            GridManager manager = GridManager.Instance;
            int filled = 0;
            for (int x = 0; x < manager.GetWidth(); x++)
                for (int z = 0; z < manager.GetHeight(); z++)
                {
                    GroundCell ground = TestField.Ground(new GridPosition(x, z));
                    if (ground == null || ground.IsLocked || ground.CurrentModifier != null) continue;
                    if (TestField.ApplyTile(new GridPosition(x, z), tile, tileRoll, level).Ok) filled++;
                }
            return Result.Success($"{filled} boş hücreye {tile.name} kondu. Grid dolu: sıradaki kartlar yükseltme verir.");
        }

        private static Result MaxAllTiles()
        {
            GridManager manager = GridManager.Instance;
            int raised = 0;
            for (int x = 0; x < manager.GetWidth(); x++)
                for (int z = 0; z < manager.GetHeight(); z++)
                {
                    GroundCell ground = TestField.Ground(new GridPosition(x, z));
                    if (ground != null && ground.CanUpgrade && ground.AddLevels(GroundCell.MaxLevel) > 0) raised++;
                }
            return Result.Success($"{raised} tile azami seviyeye çıkarıldı (oyunun yükseltme yolu). Boş hücre de yoksa sıradaki kartlar temel güç verir.");
        }
    }
}
