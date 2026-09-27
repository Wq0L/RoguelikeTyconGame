using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillTreeManager))]
public sealed class SkillTreeManagerDebugEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var manager = (SkillTreeManager)target;
        if (!manager.DebugUnlockAllEnabled) return;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Yalnız Play Mode: bütün skill'leri son kademeye getirir; kaynak harcamaz. Tiki kapatmak alınan skill'leri geri almaz. Yeni run normal sıfırlamayı kullanır.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || StatManager.Instance == null || UnlockManager.Instance == null))
            if (GUILayout.Button("TÜM SKILL TREE'Yİ TAMAMLA (DEBUG)", GUILayout.Height(32)))
                manager.DebugMaxAllSkills();
    }

    [MenuItem("Tools/Debug/Select Skill Tree Manager")]
    static void SelectManager()
    {
        var manager = Object.FindFirstObjectByType<SkillTreeManager>(FindObjectsInactive.Include);
        if (manager != null) { Selection.activeGameObject = manager.gameObject; EditorGUIUtility.PingObject(manager); }
        else Debug.LogWarning("SkillTreeManager bulunamadı. GameScene'i açıp tekrar dene.");
    }
}
