using UnityEditor;
using UnityEngine;

// Tools > Run Profili: hangi run profilinin oynanacağını seçer (Assets/Resources/RunProfileSelection.asset).
// Sahneye dokunmaz. Seçim Play'e basmadan önce yapılır; oyun içinde değişmez.
public static class RunProfileMenu
{
    private const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    private const string Folder = "Assets/ScriptableObjects/RunProfiles/";
    private const string Root = "Tools/Run Profili/";

    [MenuItem(Root + "Prototip · 10 round (normal ekonomi)", priority = 1)]
    private static void Prototype() => Select(Folder + "Prototip10.asset");

    [MenuItem(Root + "Prototip · 10 round (debug bütçe)", priority = 2)]
    private static void PrototypeDebug() => Select(Folder + "Prototip10_DebugButce.asset");

    [MenuItem(Root + "Uzmanlaşma testi · 20 round (normal ekonomi)", priority = 10)]
    private static void Specialization() => Select(Folder + "Uzmanlasma20.asset");

    [MenuItem(Root + "Uzun run · 130 round (normal ekonomi)", priority = 20)]
    private static void LongRun() => Select(Folder + "UzunRun130.asset");

    [MenuItem(Root + "Profil yok (sahnedeki ayarlar)", priority = 40)]
    private static void None() => Select(null);

    [MenuItem(Root + "Prototip · 10 round (normal ekonomi)", true)]
    private static bool PrototypeCheck() => Check(Folder + "Prototip10.asset");
    [MenuItem(Root + "Prototip · 10 round (debug bütçe)", true)]
    private static bool PrototypeDebugCheck() => Check(Folder + "Prototip10_DebugButce.asset");
    [MenuItem(Root + "Uzmanlaşma testi · 20 round (normal ekonomi)", true)]
    private static bool SpecializationCheck() => Check(Folder + "Uzmanlasma20.asset");
    [MenuItem(Root + "Uzun run · 130 round (normal ekonomi)", true)]
    private static bool LongRunCheck() => Check(Folder + "UzunRun130.asset");
    [MenuItem(Root + "Profil yok (sahnedeki ayarlar)", true)]
    private static bool NoneCheck() => Check(null);

    private static bool Check(string path)
    {
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        string current = selection != null && selection.active != null ? AssetDatabase.GetAssetPath(selection.active) : null;
        Menu.SetChecked(Root + Label(path), current == path);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static string Label(string path) => path == null ? "Profil yok (sahnedeki ayarlar)"
        : path.EndsWith("Prototip10.asset") ? "Prototip · 10 round (normal ekonomi)"
        : path.EndsWith("Prototip10_DebugButce.asset") ? "Prototip · 10 round (debug bütçe)"
        : path.EndsWith("Uzmanlasma20.asset") ? "Uzmanlaşma testi · 20 round (normal ekonomi)"
        : "Uzun run · 130 round (normal ekonomi)";

    private static void Select(string path)
    {
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        if (selection == null) { Debug.LogError("RunProfileSelection.asset bulunamadı: " + SelectionPath); return; }
        selection.active = path != null ? AssetDatabase.LoadAssetAtPath<RunProfileSO>(path) : null;
        if (path != null && selection.active == null) { Debug.LogError("Run profili bulunamadı: " + path); return; }
        EditorUtility.SetDirty(selection);
        AssetDatabase.SaveAssetIfDirty(selection);
        Debug.Log("Run profili: " + (selection.active != null ? selection.active.displayName : "yok (sahnedeki ayarlar)"), selection);
    }
}
