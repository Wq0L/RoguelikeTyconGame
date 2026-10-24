using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MechanicsTestLab
{
    // E · Boss ve tarla olayları.
    public sealed partial class LabWindow
    {
        private readonly List<SegmentEventSO> bossList = new();
        private int bossIndex, bossRound;
        private bool ignoreEligibility;
        private float eventDelay = 1f;
        private int eventSeed = 1;
        private bool eventAtSelection;

        private void DrawEvents()
        {
            RoundManager rounds = RoundManager.Instance;
            if (rounds == null) return;

            Header("Boss (oyunun boss akışı)");
            if (bossList.Count == 0) LabCatalog.Bosses(TestSession.Profile, bossList);
            if (bossList.Count == 0) EditorGUILayout.LabelField("Bu profilin boss havuzu yok.");
            else
            {
                var names = new string[bossList.Count];
                for (int i = 0; i < names.Length; i++) names[i] = $"{bossList[i].displayName} ({bossList[i].name})";
                bossIndex = Mathf.Clamp(EditorGUILayout.Popup("Boss", bossIndex, names), 0, names.Length - 1);
                if (bossRound <= 0 || !rounds.IsBossRound(bossRound)) bossRound = Mathf.Max(1, TestRewards.NextBossRound(rounds, rounds.CurrentRound));
                EditorGUILayout.BeginHorizontal();
                bossRound = Mathf.Clamp(EditorGUILayout.IntField("Boss round'u", bossRound), 1, rounds.MaxRounds);
                if (GUILayout.Button("Sonraki", GUILayout.Width(70f)))
                {
                    int next = TestRewards.NextBossRound(rounds, bossRound + 1);
                    bossRound = next > 0 ? next : TestRewards.NextBossRound(rounds, 1);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Boss round'ları: " + TestRewards.BossRounds(rounds), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("Kural: " + bossList[bossIndex].ruleText, EditorStyles.wordWrappedMiniLabel);
                ignoreEligibility = EditorGUILayout.ToggleLeft("Havuz uygunluğunu yok say (normal oyunda oluşmayan kombinasyon)", ignoreEligibility);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Bu boss'u bu round'da başlat", GUILayout.Height(24f))) Say(TestEvents.StartBoss(bossList[bossIndex], bossRound, ignoreEligibility));
                if (GUILayout.Button("Boss etkisini temizle", GUILayout.Height(24f))) Say(TestEvents.ClearBoss());
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Boss, oturumun çalışma zamanı profil kopyasının takvimine o tarih için yazılır ve round oyunun geçişiyle başlar; " +
                                           "tarla, ödüller ve statlar korunur. Aynı tarlada başka boss denemek için boss'u değiştirip yeniden başlat.", EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.HelpBox(TestEvents.BossStatus(), MessageType.None);

            Header("Tarla olayları (oyunun olay yürütücüsü)");
            FieldEventDirector director = FieldEventDirector.Instance;
            if (director == null) { EditorGUILayout.LabelField("Tarla olayı yürütücüsü yok."); return; }
            if (director.Plan == null) { EditorGUILayout.HelpBox("Bu profilin tarla olayı planı yok: olaylar bu profilde kapalı.", MessageType.Info); return; }
            eventDelay = EditorGUILayout.Slider("Başlangıç gecikmesi (sn)", eventDelay, 0f, 6f);
            eventSeed = EditorGUILayout.IntField("Bölge seed'i", eventSeed);
            using (new EditorGUI.DisabledScope(!hasSelection))
                eventAtSelection = EditorGUILayout.ToggleLeft(hasSelection
                    ? $"Bölge seçili hücreyi içersin: ({anchor.x}, {anchor.z}) — geçerli değilse olay başlamaz, bölge başka yere taşınmaz"
                    : "Bölge seçili hücreyi içersin (önce gridden hücre seç)", eventAtSelection && hasSelection);
            using (new EditorGUI.DisabledScope(!rounds.IsRoundActive))
            {
                EditorGUILayout.BeginHorizontal();
                for (int i = 0; i < TestEvents.EventNames.Length; i++)
                    if (GUILayout.Button(TestEvents.EventNames[i], GUILayout.Height(24f)))
                        Say(TestEvents.StartEvent((FieldEventKind)i, eventDelay, eventSeed, eventAtSelection && hasSelection ? anchor : (GridPosition?)null));
                EditorGUILayout.EndHorizontal();
            }
            if (rounds.CurrentRound < director.Plan.firstRound)
                EditorGUILayout.LabelField($"Not: normal takvimde tarla olayları R{director.Plan.firstRound}'dan önce çıkmaz; burada elle başlatılır.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.HelpBox(TestEvents.EventStatus(), MessageType.None);
            if (GUILayout.Button("Olayı temizle (oyunun temizlik yolu)")) Say(TestEvents.EndEvent());
            EditorGUILayout.LabelField("Aynı anda tek tarla olayı kuralı sürer: yeni olay öncekini kapatır. Fazlar oyun zamanıyla ilerler (zaman kontrolü olayı da " +
                                       "yavaşlatır / durdurur). Hedefler yalnız doğrudan saldırıyla vurulur: 'A · Oturum > Tek saldırı' ya da fareyle normal oyun. " +
                                       "Süre dolmasını görmek için hiç vurmadan bekle.", EditorStyles.wordWrappedMiniLabel);
        }
    }
}
