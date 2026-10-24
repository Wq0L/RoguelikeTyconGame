using UnityEditor;
using UnityEngine;

namespace MechanicsTestLab
{
    // C · Oyuncu ve statlar: test stat katkıları (ayrı ve görünür kaynak), level / XP / kaynak.
    public sealed partial class LabWindow
    {
        private static readonly string[] OperationNames = { "Düz ekle (Flat)", "Toplanan yüzde (AddPercent; 0,10 = +%10)", "Ayrı çarpan (MorePercent; 0,10 = ×1,10)", "Tabanı değiştir (Set)" };
        private const int ChoiceWarning = 30, PreviewLimit = 100000;

        private int statIndex, operationIndex;
        private float statValue = 1f;
        private int levelValue = 10;
        private double xpValue = 100;
        private int goldValue, ironValue, stoneValue;
        private bool resourcesRead;

        private void DrawStats()
        {
            PlanterBrain planter = hasSelection ? TestField.PlanterAt(anchor) : null;
            Header("Stat tablosu (sonuç: oyunun çözülmüş değeri)");
            EditorGUILayout.LabelField(planter != null
                ? $"Saksı kapsamlı satırlar seçili saksı için: ({anchor.x}, {anchor.z})."
                : "Saksı kapsamlı satırların sonucu saksıya göre değişir: gridden bir saksı seç ('B · Tarla').", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Stat", EditorStyles.miniBoldLabel, GUILayout.MinWidth(230f));
            GUILayout.Label("Taban", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
            GUILayout.Label("Test'siz", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
            GUILayout.Label("Sonuç", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
            foreach ((StatType stat, StatTarget target, string label) in TestStats.Offered)
            {
                bool planterScope = target == StatTarget.Planter;
                string baseText = "—", withoutText = "—", finalText = "—";
                if (planterScope)
                {
                    if (planter != null && planter.EditorData != null)
                    {
                        float baseValue = planter.EditorData.GetBaseStat(stat);
                        baseText = baseValue.ToString("0.###");
                        // Rezonanstan önceki değer için aynı hesaplayıcı; sonuç saksının çözülmüş değeri (rezonans dahil).
                        withoutText = StatCalculator.Calculate(baseValue, stat, StatTarget.Planter, TestStats.GlobalsWithoutTest(), planter.LocalModifiers).ToString("0.###");
                        finalText = planter.GetFinalStat(stat).ToString("0.###");
                    }
                }
                else
                {
                    float baseValue = TestStats.Base(stat);
                    if (!float.IsNaN(baseValue))
                    {
                        baseText = baseValue.ToString("0.###");
                        withoutText = TestStats.WithoutTest(stat, target, baseValue).ToString("0.###");
                        finalText = TestStats.Final(stat, target).ToString("0.###");
                    }
                }
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.MinWidth(230f));
                GUILayout.Label(baseText, EditorStyles.miniLabel, GUILayout.Width(70f));
                GUILayout.Label(withoutText, EditorStyles.miniLabel, GUILayout.Width(70f));
                GUILayout.Label(finalText, withoutText != finalText ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel, GUILayout.Width(70f));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField("'Test'siz': aynı hesaplayıcı, test katkıları çıkarılmış listeyle (saksıda rezonanstan önce). Davranış hasarı ayrı bir stat değildir: " +
                                       "tabanı Doğrudan hasar statıdır, çarpanı rezonans × uzmanlaşma × boss ödülüdür ('F · İnceleme'de saksı başına yazılır). " +
                                       "Kaynak dökümü için 'F · İnceleme > Stat kaynağı'.", EditorStyles.wordWrappedMiniLabel);

            Header("Test katkısı ekle");
            var names = new string[TestStats.Offered.Length];
            for (int i = 0; i < names.Length; i++) names[i] = TestStats.Offered[i].label;
            statIndex = EditorGUILayout.Popup("Stat", statIndex, names);
            operationIndex = EditorGUILayout.Popup("İşlem", operationIndex, OperationNames);
            statValue = EditorGUILayout.FloatField("Değer", statValue);
            (StatType stat, StatTarget target, string label) chosen = TestStats.Offered[statIndex];
            if (chosen.target == StatTarget.Planter)
                EditorGUILayout.LabelField("Bu katkı global bir saksı katkısıdır: BÜTÜN saksılara uygulanır. Tek saksıya özel etki için o saksının altına tile koy.", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Katkıyı ekle"))
                Say(TestStats.Add(new StatModifier { statType = chosen.stat, target = chosen.target, operation = (ModifierOperation)operationIndex, value = statValue }));

            Header($"Test katkıları ({TestStats.Contributions.Count})");
            if (TestStats.Contributions.Count == 0) EditorGUILayout.LabelField("Yok.");
            for (int i = 0; i < TestStats.Contributions.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(TestStats.Describe(TestStats.Contributions[i]), EditorStyles.miniLabel);
                if (GUILayout.Button("Kaldır", GUILayout.Width(60f))) { Say(TestStats.RemoveAt(i)); GUIUtility.ExitGUI(); }
                EditorGUILayout.EndHorizontal();
            }
            using (new EditorGUI.DisabledScope(TestStats.Contributions.Count == 0))
                if (GUILayout.Button("Test katkısını sıfırla (yalnız test katkıları kalkar)")) Say(TestStats.Clear());

            DrawProgress();
        }

        private void DrawProgress()
        {
            ProgressionManager progression = ProgressionManager.Instance;
            RoundManager rounds = RoundManager.Instance;
            ResourceManager resources = ResourceManager.Instance;
            if (progression == null || rounds == null || resources == null) return;

            Header("Level ve XP");
            EditorGUILayout.LabelField($"Level {progression.CurrentLevel} · XP {progression.StoredXP:0.#} / {progression.XPToNextLevel:0.#} · bekleyen kart seçimi {rounds.PendingCardSelections}");
            EditorGUILayout.LabelField($"Level başına seçim hakkı: {rounds.ChoicesPerLevel} = temel {rounds.BaseChoicesPerLevel} {(BossRewardManager.LevelChoiceDelta >= 0 ? "+" : "−")} " +
                                       $"{Mathf.Abs(BossRewardManager.LevelChoiceDelta)} (ödüllerden; oyunun normal hesabı, buradan değiştirilmez)", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.BeginHorizontal();
            levelValue = Mathf.Max(1, EditorGUILayout.IntField("Level ata", levelValue));
            if (GUILayout.Button("Ata (kart hakkı YOK)", GUILayout.Width(160f))) Say(TestStats.SetLevel(levelValue));
            EditorGUILayout.EndHorizontal();
            xpValue = System.Math.Max(0d, EditorGUILayout.DoubleField("XP ver (normal akış)", xpValue));
            int levels = 0, choices = 0;
            bool capped = false;
            bool previewed = xpValue > 0d && TestStats.PreviewXP(xpValue, PreviewLimit, out levels, out choices, out capped);
            EditorGUILayout.LabelField(previewed
                ? $"Önizleme: {(capped ? "en az " : "")}{levels} level → {(capped ? "en az " : "")}{choices} kart seçim hakkı (round sonunda kart ekranı bu kadar kez açılır)."
                : "Önizleme yok (XP 0 ya da level işleme durmuş).", choices > ChoiceWarning ? EditorStyles.boldLabel : EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(!previewed))
                if (GUILayout.Button("XP'yi ver"))
                {
                    bool confirmed = choices <= ChoiceWarning || EditorUtility.DisplayDialog("Çok sayıda kart seçimi",
                        $"Bu XP {(capped ? "en az " : "")}{levels} level ve {(capped ? "en az " : "")}{choices} kart seçim hakkı doğurur. Round sonunda kart ekranı o kadar kez açılır. Devam edilsin mi?", "XP'yi ver", "Vazgeç");
                    if (confirmed) Say(TestStats.GiveXP(xpValue));
                }
            EditorGUILayout.LabelField("Kart ekranı oyunun normal yoluyla, round bitince açılır: 'A · Oturum > Round'u şimdi bitir'.", EditorStyles.wordWrappedMiniLabel);

            Header("Kaynaklar");
            EditorGUILayout.LabelField($"Şu an: altın {resources.GetResourceAmount(ResourceType.Gold):N0} · demir {resources.GetResourceAmount(ResourceType.Iron):N0} · " +
                                       $"taş {resources.GetResourceAmount(ResourceType.Stone):N0}");
            if (!resourcesRead) ReadResources(resources);
            goldValue = Mathf.Max(0, EditorGUILayout.IntField("Altın", goldValue));
            ironValue = Mathf.Max(0, EditorGUILayout.IntField("Demir", ironValue));
            stoneValue = Mathf.Max(0, EditorGUILayout.IntField("Taş", stoneValue));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Kaynak miktarlarını uygula"))
            {
                TestStats.SetResource(ResourceType.Gold, goldValue);
                TestStats.SetResource(ResourceType.Iron, ironValue);
                TestStats.SetResource(ResourceType.Stone, stoneValue);
                Say(Result.Success($"Kaynaklar: altın {resources.GetResourceAmount(ResourceType.Gold):N0} · demir {resources.GetResourceAmount(ResourceType.Iron):N0} · taş {resources.GetResourceAmount(ResourceType.Stone):N0}"));
            }
            if (GUILayout.Button("Alanlara canlı değerleri yaz", GUILayout.Width(190f))) ReadResources(resources);
            EditorGUILayout.EndHorizontal();
        }

        private void ReadResources(ResourceManager resources)
        {
            goldValue = resources.GetResourceAmount(ResourceType.Gold);
            ironValue = resources.GetResourceAmount(ResourceType.Iron);
            stoneValue = resources.GetResourceAmount(ResourceType.Stone);
            resourcesRead = true;
        }
    }
}
