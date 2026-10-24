using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MechanicsTestLab
{
    // F · İnceleme ve olay kaydı: gerçek durumu okur (TestInspector) ve kaydı gösterir (TestLog).
    public sealed partial class LabWindow
    {
        private const int ShownEntries = 250;
        private readonly List<TestLog.Entry> logView = new();
        private TestLog.Kind logMask = TestLog.Kind.All & ~TestLog.Kind.Stat;
        private string logSearch = "";
        private int logAttack;
        private Vector2 logScroll, inspectScroll, runtimeScroll;
        private int explainIndex;
        private bool explainPlanter = true, showGrid = true, showRuntime = true, showLog = true;
        private int logVersion = -1;

        private void DrawInspect()
        {
            showGrid = EditorGUILayout.Foldout(showGrid, "Grid (hücre seç)", true);
            if (showGrid) DrawGrid();

            Header("Seçili hücre · saksı · bitki (gerçek durum)");
            if (!hasSelection) EditorGUILayout.LabelField("Gridden bir hücre seç.");
            else
            {
                text.Clear();
                TestInspector.DescribeCell(anchor, text);
                inspectScroll = EditorGUILayout.BeginScrollView(inspectScroll, GUILayout.MinHeight(220f), GUILayout.MaxHeight(380f));
                EditorGUILayout.TextArea(text.ToString().TrimEnd('\n'), wrapStyle);
                EditorGUILayout.EndScrollView();
            }

            Header("Stat kaynağı: \"bu stat neden bu değer?\"");
            var names = new string[TestStats.Offered.Length];
            for (int i = 0; i < names.Length; i++) names[i] = TestStats.Offered[i].label;
            explainIndex = EditorGUILayout.Popup("Stat", explainIndex, names);
            (StatType stat, StatTarget target, string label) chosen = TestStats.Offered[explainIndex];
            PlanterBrain planter = hasSelection ? TestField.PlanterAt(anchor) : null;
            using (new EditorGUI.DisabledScope(planter == null))
                explainPlanter = EditorGUILayout.ToggleLeft(planter != null ? $"Seçili saksının kapsamında incele: ({anchor.x}, {anchor.z})" : "Saksı kapsamı için gridden bir saksı seç", explainPlanter);
            text.Clear();
            TestInspector.ExplainStat(chosen.stat, chosen.target, explainPlanter ? planter : null, text);
            EditorGUILayout.TextArea(text.ToString().TrimEnd('\n'), wrapStyle);

            showRuntime = EditorGUILayout.Foldout(showRuntime, "Oturum durumu: round, beklemeler, kapasiteler, aktif etkiler", true);
            if (showRuntime)
            {
                text.Clear();
                TestInspector.DescribeRuntime(text);
                runtimeScroll = EditorGUILayout.BeginScrollView(runtimeScroll, GUILayout.MinHeight(160f), GUILayout.MaxHeight(340f));
                EditorGUILayout.TextArea(text.ToString().TrimEnd('\n'), wrapStyle);
                EditorGUILayout.EndScrollView();
            }

            showLog = EditorGUILayout.Foldout(showLog, $"Olay kaydı ({TestLog.Count} kayıt; toplam {TestLog.Total}, düşen {TestLog.Dropped}, sınır {TestLog.Capacity})", true);
            if (showLog) DrawLog();
        }

        private void DrawLog()
        {
            DrawLogLevel();
            EditorGUI.BeginChangeCheck();
            logMask = (TestLog.Kind)EditorGUILayout.EnumFlagsField("Türler", logMask);
            logSearch = EditorGUILayout.TextField("Metin süzgeci", logSearch);
            logAttack = Mathf.Max(0, EditorGUILayout.IntField("Saldırı kimliği (0: hepsi)", logAttack));
            if (EditorGUI.EndChangeCheck()) logVersion = -1;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Temizle")) { TestLog.Clear(); logVersion = -1; }
            if (GUILayout.Button("Dışarı aktar (Logs/MechanicsTestLab)")) Say(TestLog.Export());
            EditorGUILayout.EndHorizontal();
            if (logVersion != TestLog.Version)
            {
                logVersion = TestLog.Version;
                TestLog.Collect(logMask, logSearch, logAttack, logView);
            }
            EditorGUILayout.LabelField($"{logView.Count} kayıt süzgeçten geçti; son {Mathf.Min(ShownEntries, logView.Count)} gösteriliyor. S = saldırıya kesin bağlı · ~S = o saldırıdan sonra " +
                                       "(kök taşımayan gecikmeli vuruş; bağ kanıtı yok)." + (TestLog.Faults > 0 ? $" · dinleyici hatası {TestLog.Faults}" : ""), EditorStyles.wordWrappedMiniLabel);
            logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.MinHeight(260f), GUILayout.MaxHeight(520f));
            for (int i = Mathf.Max(0, logView.Count - ShownEntries); i < logView.Count; i++) GUILayout.Label(TestLog.Format(logView[i]), monoStyle);
            EditorGUILayout.EndScrollView();
        }
    }
}
