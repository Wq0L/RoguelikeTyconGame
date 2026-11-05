using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MechanicsTestLab
{
    // I · Diyalog (Faz 6, 5 Kasım 2026): öğretim, hikâye ve tepki konuşmalarını tam run oynamadan denemek için.
    // Komutlar TestDialogue'da; kayıt değiştirenler yalnız bellekteki test kaydına yazar.
    public sealed partial class LabWindow
    {
        private static readonly string[] CategoryFilter = { "Hepsi", "Öğretim (Guide)", "Hikâye (Story)", "Adlandırma (Naming)", "Tepki (Reaction)" };
        private static readonly DialogueCategory[] CategoryOf = { DialogueCategory.Guide, DialogueCategory.Guide, DialogueCategory.Story, DialogueCategory.Naming, DialogueCategory.Reaction };
        private static readonly DialogueTrigger[] Triggers = BuildTriggers();
        private static readonly string[] TriggerNames = Array.ConvertAll(Triggers, t => t.ToString());

        private int dialogueFilter, dialogueTrigger;
        private string dialogueSearch = "";
        private bool dialogueMarkSeen;
        private readonly HashSet<string> dialogueOpen = new();

        private static DialogueTrigger[] BuildTriggers()
        {
            var list = new List<DialogueTrigger>();
            foreach (DialogueTrigger t in Enum.GetValues(typeof(DialogueTrigger)))
                if (t != DialogueTrigger.None && t != DialogueTrigger.StoryBeat) list.Add(t);
            return list.ToArray();
        }

        private void DrawDialogue()
        {
            DialogueDirector director = TestDialogue.Director;
            if (director == null) { EditorGUILayout.HelpBox("Diyalog yöneticisi yok.", MessageType.Warning); return; }
            EditorGUILayout.HelpBox(TestDialogue.Status(), MessageType.None);

            Header("Yönetici");
            bool on = EditorGUILayout.ToggleLeft("Diyalog yöneticisi bu oturumda çalışsın (öğretimler ve hikâye oyundaki tetiklerle kendiliğinden çıkar)", TestDialogue.Enabled);
            if (on != TestDialogue.Enabled) Say(TestDialogue.SetEnabled(on));
            EditorGUILayout.BeginHorizontal();
            dialogueTrigger = EditorGUILayout.Popup("Oyunun tetiği", dialogueTrigger, TriggerNames);
            using (new EditorGUI.DisabledScope(!TestDialogue.Enabled))
                if (GUILayout.Button("Şimdi yayınla", GUILayout.Width(110f))) Say(TestDialogue.Raise(Triggers[dialogueTrigger]));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Tetik, oyundaki gibi uygun konuşmayı seçer (aşama, görüldü ve ekran kuralları geçerli). Öğretimi yeniden görmek için önce " +
                                       "'Görülenleri sıfırla'. Kapalıyken konuşmalar yalnız aşağıdaki ▶ düğmeleriyle oynar.", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Açık kutuları kapat")) Say(TestDialogue.CloseAll());

            Header("Hikâye");
            EditorGUILayout.BeginHorizontal();
            for (int s = 0; s <= DialogueProgress.MaxStage; s++)
            {
                Color normal = GUI.backgroundColor;
                if (DialogueProgress.Stage == s) GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
                if (GUILayout.Button(TestDialogue.StageNames[s], GUILayout.Height(22f))) Say(TestDialogue.SetStage(s));
                GUI.backgroundColor = normal;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Aşama Eri'nin görünüşünü (kutuda ve gezegende) ve konuşma kutusunun grisini hemen değiştirir. Lab'de geri de alınabilir.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Sıradaki taşı kazan")) Say(TestDialogue.WinNextStone());
            if (GUILayout.Button("Sıradaki hikâyeyi oynat")) Say(TestDialogue.PlayNextStory());
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Taş 1 cevabı: " + (DialogueProgress.Choice(DialogueProgress.TrialPassed) ?? "yok"), GUILayout.Width(150f));
            if (GUILayout.Button("A")) Say(TestDialogue.SetTrialChoice("A"));
            if (GUILayout.Button("B")) Say(TestDialogue.SetTrialChoice("B"));
            if (GUILayout.Button("Sil")) Say(TestDialogue.SetTrialChoice(null));
            EditorGUILayout.EndHorizontal();
            for (int i = 0; i < DialogueProgress.StoryOrder.Length; i++)
            {
                string id = DialogueProgress.StoryOrder[i];
                bool reached = DialogueProgress.HasMilestone(id);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{i + 1,2}. {id}", GUILayout.Width(150f));
                EditorGUILayout.LabelField(TestDialogue.StoryState(id), GUILayout.Width(130f));
                if (GUILayout.Button(reached ? "Taşı sil" : "Taşı kazan", GUILayout.Width(80f))) Say(TestDialogue.SetStone(id, !reached));
                using (new EditorGUI.DisabledScope(!director.EditorTryGet(id, out DialogueEntry e)))
                    if (GUILayout.Button("▶", GUILayout.Width(28f))) Say(TestDialogue.Play(e, -1, dialogueMarkSeen));
                EditorGUILayout.EndHorizontal();
            }

            Header("Konuşmalar");
            EditorGUILayout.BeginHorizontal();
            dialogueFilter = EditorGUILayout.Popup(dialogueFilter, CategoryFilter, GUILayout.Width(170f));
            dialogueSearch = EditorGUILayout.TextField(dialogueSearch);
            EditorGUILayout.EndHorizontal();
            dialogueMarkSeen = EditorGUILayout.ToggleLeft("▶ ile oynatılan konuşmayı görüldü say (kapalı: kayıt değişmez)", dialogueMarkSeen);
            string search = dialogueSearch.Trim();
            foreach (DialogueEntry entry in director.EditorEntries)
            {
                if (entry == null || entry.lines == null) continue;
                if (dialogueFilter > 0 && entry.category != CategoryOf[dialogueFilter]) continue;
                string first = entry.lines.Length > 0 ? GameText.Get(entry.lines[0].textKey) : "";
                if (search.Length > 0 && entry.id.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 && first.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool open = dialogueOpen.Contains(entry.id);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("▶", GUILayout.Width(28f))) Say(TestDialogue.Play(entry, -1, dialogueMarkSeen));
                bool now = EditorGUILayout.Foldout(open, $"{entry.id} · {entry.category} · {entry.trigger} · {entry.size}" +
                                                         $"{(entry.frequency == DialogueFrequency.Repeat ? " · tekrar" : "")} · aşama {entry.minStage}–{entry.maxStage}" +
                                                         $"{(DialogueProgress.Seen(entry.id) ? " · görüldü" : "")}", true);
                EditorGUILayout.EndHorizontal();
                if (now != open) { if (now) dialogueOpen.Add(entry.id); else dialogueOpen.Remove(entry.id); }
                if (!now)
                {
                    EditorGUILayout.LabelField("    " + Short(first), EditorStyles.miniLabel);
                    continue;
                }
                for (int l = 0; l < entry.lines.Length; l++)
                {
                    DialogueLine line = entry.lines[l];
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(20f);
                    if (GUILayout.Button("▶", GUILayout.Width(28f))) Say(TestDialogue.Play(entry, l, false));
                    string extra = (line.setStage >= 0 ? $" [aşama → {line.setStage}]" : "") + (line.HasChoice ? " [şıklı]" : "") +
                                   (!string.IsNullOrEmpty(line.onlyIfChoice) ? $" [yalnız {line.onlyIfChoice}]" : "");
                    EditorGUILayout.LabelField($"{l + 1}. {GameText.Get(line.textKey)}{extra}", wrapStyle);
                    EditorGUILayout.EndHorizontal();
                }
                if (!string.IsNullOrEmpty(entry.requiresSeen)) EditorGUILayout.LabelField("    Önce görülmeli: " + entry.requiresSeen, EditorStyles.miniLabel);
            }

            Header("Sıfırla (yalnız bellekteki test kaydı)");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Görülenleri sıfırla (öğretimler yeniden çıkar)")) Say(TestDialogue.ResetSeen());
            if (GUILayout.Button("Hikâyeyi baştan al (taşlar, cevaplar, aşama 0)")) Say(TestDialogue.ResetStory());
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Oturum gerçek kaydı okur ama hiç yazmaz: buradaki değişiklikler Play bitince gider. Oturumu yeniden kurmak bellekteki ilerlemeyi silmez.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private static string Short(string s)
        {
            s = (s ?? "").Replace("<br>", " ");
            return s.Length > 110 ? s.Substring(0, 107) + "…" : s;
        }
    }
}
