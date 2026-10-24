using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SoundManagerSceneSetup
{
    [MenuItem("Tools/Sound/Configure Game Scene %#&s")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play Mode before configuring scene audio.");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Additive);
        AssetDatabase.Refresh();
        SoundManager manager = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<SoundManager>(true))
            {
                if (manager != null) throw new System.InvalidOperationException("Multiple scene SoundManagers.");
                manager = candidate;
            }
        if (manager == null)
        {
            var host = new GameObject("SoundManager");
            SceneManager.MoveGameObjectToScene(host, scene);
            Undo.RegisterCreatedObjectUndo(host, "Create scene SoundManager");
            manager = Undo.AddComponent<SoundManager>(host);
        }
        var serialized = new SerializedObject(manager);
        Bind(serialized, "placeItem", "place-item", 0.7f);
        Bind(serialized, "plantDamage", "cartoon-roguelike-hit-v7", 0.3f, "Assets/Audio/Generated/");
        Bind(serialized, "invalidPlacement", "ui-error", 0.65f);
        Bind(serialized, "cancelPlacement", "ui-back", 0.65f);
        var entries = serialized.FindProperty("entries");
        entries.arraySize = 1;
        var entry = entries.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("clip").objectReferenceValue = RequiredClip("Assets/Resources/Music/RoundMusic.wav");
        entry.FindPropertyRelative("resourcePath").stringValue = "Music/RoundMusic";
        entry.FindPropertyRelative("weight").floatValue = 1f;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new System.IO.IOException("Scene save failed.");
        Selection.activeGameObject = manager.gameObject;
        Directory.CreateDirectory("Docs/Audio");
        File.WriteAllText("Docs/Audio/SceneSetupVerification.txt",
            "PASS: GameScene has one SoundManager; four required SFX clips and one weight-1 music clip assigned; scene saved.\n");
        Debug.Log("SoundManager scene setup: PASS");
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }

    private static AudioClip RequiredClip(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) throw new System.IO.FileNotFoundException("Missing audio clip", path);
        return clip;
    }

    private static void Bind(SerializedObject manager, string property, string file, float gain, string folder = "Assets/Audio/CozyFarm/")
    {
        var cue = manager.FindProperty(property);
        cue.FindPropertyRelative("clip").objectReferenceValue = RequiredClip(folder + file + ".wav");
        cue.FindPropertyRelative("volume").floatValue = gain;
        cue.FindPropertyRelative("pitch").floatValue = 1f;
    }
}
