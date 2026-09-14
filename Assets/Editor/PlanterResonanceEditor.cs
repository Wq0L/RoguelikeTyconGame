using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlanterBrain))]
public class PlanterResonanceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        PlanterBrain planter = (PlanterBrain)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Rezonans prototipi", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || planter.OccupiedGrids.Count == 0))
        {
            if (GUILayout.Button("Tile ve rezonans hesabini yenile")) planter.RefreshTileBuffs();
            EditorGUILayout.HelpBox("Ornekler secili saksinin mevcut tile'larini temizleyip yeni bir duzen kurar. Yalniz Play Mode; kalici kayit yapmaz.", MessageType.Info);
            Example(planter, "2 Odak", "Damage", 2);
            Example(planter, "3 Odak", "Damage", 3);
            Example(planter, "4 Odak", "Damage", 4);
            Example(planter, "2 Water (XP rezonansi x2)", "Water", 2);
            Example(planter, "3 Water (XP rezonansi x10)", "Water", 3);
            Example(planter, "3 Fertile (sure rezonansi x0.5)", "Fertile", 3);
        }
    }

    private static void Example(PlanterBrain planter, string label, string family, int count)
    {
        using (new EditorGUI.DisabledScope(planter.OccupiedGrids.Count < count))
        {
            if (!GUILayout.Button(label)) return;
            TileModifierSO tile = AssetDatabase.LoadAssetAtPath<TileModifierSO>(
                $"Assets/ScriptableObjects/GridModifiers/{family}/{family}-Common.asset");
            if (tile == null) { Debug.LogError("Ornek tile bulunamadi: " + family); return; }
            for (int i = 0; i < planter.OccupiedGrids.Count; i++)
                planter.OccupiedGrids[i].GetGroundCellCached()?.ApplyModifier(i < count ? tile : null);
            planter.RefreshTileBuffs();
        }
    }
}
