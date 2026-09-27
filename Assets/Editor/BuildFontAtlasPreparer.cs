using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Ships the game fonts with populated atlases instead of empty ones.
// TMP's "Clear Dynamic Data On Build" emptied every atlas, so the EXE added hundreds of glyphs at runtime,
// re-uploading the same atlas many times in one frame (scene load, planter shop). That burst is the known
// trigger of the D3D12ScratchAllocator::DestroyScratch crash (Unity UUM-140564).
public sealed class BuildFontAtlasPreparer : IPreprocessBuildWithReport
{
    // Runs before TMP_PreBuildProcessor (0), which would otherwise clear the atlases.
    public int callbackOrder => -100;

    static readonly string[] FontGuids =
    {
        "125cb55b44b24c4393181402bc6200e6", // Bangers SDF — damage numbers
        "404b2d578f83a5b4d88295a467c66f15", // LilitaOne-Regular SDF — headings
        "cda3c69f5f38ca7438e47be88545601d", // Barlow-SemiBold SDF — popup text
        "87c7276b22e4ae3449a39f465315d554", // Super Popstar SDF — TMP default font
        "2e498d1c8094910479dc3e1b768306a4", // LiberationSans SDF - Fallback
    };

    // Turkish letters and the symbols found in scripts, scenes, prefabs and ScriptableObjects.
    const string ExtraCharacters = "çğıöşüÇĞİÖŞÜ→←×·—–−’‘“”…•°";

    public static string Characters
    {
        get
        {
            var builder = new StringBuilder(128);
            for (char c = ' '; c <= '~'; c++) builder.Append(c);
            return builder.Append(ExtraCharacters).ToString();
        }
    }

    public void OnPreprocessBuild(BuildReport report) => Prepare(false);

    [MenuItem("Tools/Fonts/Prepare Build Font Atlases")]
    static void PrepareFromMenu() => Prepare(true);

    public static int Prepare(bool verbose)
    {
        int changed = 0;
        string characters = Characters;
        foreach (string guid in FontGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var font = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null) continue;

            bool dirty = false;
            var serialized = new SerializedObject(font);
            var clearOnBuild = serialized.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearOnBuild != null && clearOnBuild.boolValue)
            {
                clearOnBuild.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                dirty = true;
            }

            if (font.atlasPopulationMode != AtlasPopulationMode.Static)
            {
                int before = font.characterTable.Count;
                font.TryAddCharacters(characters, out string missing);
                dirty |= font.characterTable.Count != before;
                // Missing glyphs are not in the source font file; TMP resolves them through fallbacks.
                if (verbose && !string.IsNullOrEmpty(missing))
                    Debug.Log($"[Fonts] {font.name}: not in source font, fallback will be used: {missing}", font);
            }

            if (!dirty) continue;
            EditorUtility.SetDirty(font);
            if (font.atlasTextures != null)
                foreach (var texture in font.atlasTextures)
                    if (texture != null) EditorUtility.SetDirty(texture);
            changed++;
        }
        if (changed > 0) AssetDatabase.SaveAssets();
        if (verbose) Debug.Log($"[Fonts] Build font atlases ready; updated fonts: {changed}");
        return changed;
    }
}
