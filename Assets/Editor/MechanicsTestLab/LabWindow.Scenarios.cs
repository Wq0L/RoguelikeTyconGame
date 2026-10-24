using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MechanicsTestLab
{
    // G · Senaryolar: hazır senaryolar, mevcut düzeni yakalama, dosyaya kaydetme / dosyadan yükleme.
    public sealed partial class LabWindow
    {
        private List<Scenario> presets;
        private string saveName = "";
        private int profileIndex = -1;
        private int seedValue = 1;

        // Oturum açıksa temiz oturumdan yeniden kurar; değilse bir sonraki test Play'inin başlangıç senaryosu olur.
        private void Use(Scenario scenario)
        {
            if (TestSession.Active) { TestLog.Clear(); Say(TestSession.Rebuild(scenario)); return; }
            TestSession.SetPending(scenario);
            Say(Result.Success($"Başlangıç senaryosu: {scenario.name}. 'A · Oturum'dan test oturumunu başlat ya da test sahnesinde Play'e bas."));
        }

        private void DrawScenarios()
        {
            presets ??= ScenarioPresets.All();
            Scenario current = TestSession.Active ? TestSession.Current : TestSession.Pending;

            Header(TestSession.Active ? "Kurulu senaryo" : "Başlangıç senaryosu");
            if (current == null) EditorGUILayout.LabelField("00 · Boş başlangıç (varsayılan)");
            else
            {
                EditorGUILayout.LabelField(current.name, EditorStyles.boldLabel);
                DescribeScenario(current);
            }

            if (TestSession.Ready)
            {
                Header("Profil ve seed (yeniden kurulumda geçerli olur)");
                IReadOnlyList<RunProfileSO> profiles = LabCatalog.Profiles;
                var names = new string[profiles.Count];
                for (int i = 0; i < names.Length; i++) { names[i] = profiles[i].name; if (profileIndex < 0 && profiles[i].name == TestSession.Current.profile) profileIndex = i; }
                profileIndex = Mathf.Clamp(EditorGUILayout.Popup("Kaynak profil", Mathf.Max(0, profileIndex), names), 0, Mathf.Max(0, names.Length - 1));
                seedValue = EditorGUILayout.IntField("Test seed'i (boss, teklif, olay bölgesi)", seedValue == 0 ? TestSession.Current.seed : seedValue);
                // Başlangıç seçimi (çiftçi / tırpan): oyunun kataloğundan; yalnız bu oturumun belleğindeki kayda yazılır.
                StartCatalogSO catalog = StartCatalogSO.Active;
                string farmerId = TestSession.Current.farmer, scytheId = TestSession.Current.scythe;
                if (catalog != null)
                {
                    farmerId = StartOptionPopup("Çiftçi", catalog.farmers, ref farmerIndex, TestSession.Current.farmer);
                    scytheId = StartOptionPopup("Tırpan", catalog.scythes, ref scytheIndex, TestSession.Current.scythe);
                    StartLoadoutManager loadout = StartLoadoutManager.Instance;
                    if (loadout != null && loadout.Farmer != null && loadout.Scythe != null)
                        EditorGUILayout.LabelField($"Bu oturumda: {loadout.Farmer.displayName} + {loadout.Scythe.displayName}", EditorStyles.miniLabel);
                }
                if (GUILayout.Button("Mevcut düzeni bu profil, seed ve başlangıç seçimiyle temiz oturumdan yeniden kur"))
                {
                    Scenario scenario = ScenarioRunner.Capture(TestSession.Current);
                    scenario.profile = names.Length > 0 ? names[profileIndex] : scenario.profile;
                    scenario.seed = seedValue == 0 ? 1 : seedValue;
                    scenario.farmer = farmerId;
                    scenario.scythe = scytheId;
                    Say(TestSession.Rebuild(scenario));
                }
                EditorGUILayout.LabelField("Seçili run profili (Tools > Run Profili) ve gerçek kayıttaki çiftçi / tırpan seçimi değişmez: profil yalnız bu oturum için kopyalanır, " +
                                           "başlangıç seçimi yalnız bellekte tutulur (kilitli seçenekler de denenebilir). Havuzunda olmayan ödüller yeni profilde kurulamaz ve raporlanır.",
                    EditorStyles.wordWrappedMiniLabel);

                Header("Kaydet (yalnız kurulum verisi; oyuncu kaydı değildir)");
                if (string.IsNullOrEmpty(saveName)) saveName = TestSession.Current.name;
                saveName = EditorGUILayout.TextField("Senaryo adı", saveName);
                if (GUILayout.Button("Mevcut düzeni yakala ve dosyaya kaydet"))
                {
                    Scenario scenario = ScenarioRunner.Capture(TestSession.Current);
                    scenario.name = saveName;
                    scenario.whatToDo = scenario.whatToSee = scenario.whatToCheck = "";
                    Say(ScenarioFiles.Save(scenario, ScenarioFiles.PathFor(saveName)));
                }
                EditorGUILayout.LabelField("Yakalanan: profil adı, seed, round, grid boyutu, tile'lar (tür, nadirlik, seviye, değerler), saksılar (tür, köşe, yön), bitkiler (tür, can), " +
                                           "test stat katkıları, ödüller ve stack'leri, level, kaynaklar, saldırı / üretim / sayaç bayrakları. Yakalanmayan: süren davranışlar, bekleyen " +
                                           "zincir / artçı / yankı işleri, tarla olayı, geçici etkiler, sayaçlar. Yükleme her zaman temiz oturuma kurar.", EditorStyles.wordWrappedMiniLabel);
            }

            Header("Kayıtlı senaryolar · " + ScenarioFiles.Directory);
            List<string> files = ScenarioFiles.List();
            if (files.Count == 0) EditorGUILayout.LabelField("Kayıtlı senaryo yok.");
            foreach (string file in files)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(file));
                if (GUILayout.Button("Kur", GUILayout.Width(60f)))
                {
                    Result loaded = ScenarioFiles.Load(file, out Scenario scenario);
                    if (loaded.Ok) Use(scenario); else Say(loaded);
                }
                EditorGUILayout.EndHorizontal();
            }

            Header("Hazır senaryolar");
            foreach (Scenario preset in presets)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(preset.name, EditorStyles.boldLabel);
                if (GUILayout.Button(TestSession.Active ? "Kur" : "Seç", GUILayout.Width(60f))) Use(preset);
                EditorGUILayout.EndHorizontal();
                DescribeScenario(preset);
                EditorGUILayout.EndVertical();
            }
        }

        private int farmerIndex = -1, scytheIndex = -1;

        // 0: kataloğun varsayılanı. Dönen: seçilen içeriğin id'si (varsayılan için boş).
        private static string StartOptionPopup<T>(string label, List<T> options, ref int index, string currentId) where T : StartOptionSO
        {
            var names = new string[options.Count + 1];
            names[0] = "Varsayılan";
            for (int i = 0; i < options.Count; i++)
            {
                names[i + 1] = options[i] != null ? $"{options[i].displayName} ({options[i].id})" : "—";
                if (index < 0 && options[i] != null && options[i].id == currentId) index = i + 1;
            }
            index = Mathf.Clamp(EditorGUILayout.Popup(label, Mathf.Max(0, index), names), 0, options.Count);
            return index == 0 || options[index - 1] == null ? "" : options[index - 1].id;
        }

        private void DescribeScenario(Scenario scenario)
        {
            if (!string.IsNullOrEmpty(scenario.whatToDo)) EditorGUILayout.LabelField("Ne yapacağım: " + scenario.whatToDo, EditorStyles.wordWrappedMiniLabel);
            if (!string.IsNullOrEmpty(scenario.whatToSee)) EditorGUILayout.LabelField("Ne görmeliyim: " + scenario.whatToSee, EditorStyles.wordWrappedMiniLabel);
            if (!string.IsNullOrEmpty(scenario.whatToCheck)) EditorGUILayout.LabelField("Neyi kontrol edeceğim: " + scenario.whatToCheck, EditorStyles.wordWrappedMiniLabel);
            text.Clear();
            text.Append("Kurulum: ").Append(scenario.profile).Append(" · R").Append(scenario.round).Append(" · grid ").Append(scenario.gridSize).Append('×').Append(scenario.gridSize)
                .Append(" · ").Append(scenario.planters.Count).Append(" saksı · ").Append(scenario.tiles.Count).Append(" tile");
            if (scenario.stats.Count > 0)
            {
                text.Append(" · test statları:");
                foreach (ScenarioStat stat in scenario.stats) text.Append(' ').Append(TestStats.Describe(stat.ToModifier())).Append(';');
            }
            if (scenario.rewards.Count > 0)
            {
                text.Append(" · ödüller:");
                foreach (ScenarioReward reward in scenario.rewards) text.Append(' ').Append(reward.id).Append(reward.stacks > 1 ? "×" + reward.stacks : "");
            }
            if (scenario.plantHealth > 0) text.Append(" · bütün bitkiler ").Append(scenario.plantHealth).Append(" can");
            text.Append(scenario.autoAttack ? " · otomatik saldırı" : " · elle saldırı").Append(scenario.aimFixed ? $" · nişan ({scenario.aimX}, {scenario.aimZ})" : " · fare");
            EditorGUILayout.LabelField(text.ToString(), EditorStyles.wordWrappedMiniLabel);
        }
    }
}
