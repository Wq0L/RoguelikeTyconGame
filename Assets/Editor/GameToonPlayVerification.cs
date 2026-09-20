using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Batch-only integration smoke check. No hooks run in a normal editor session.
[InitializeOnLoad]
public static class GameToonPlayVerification
{
    const string Key = "GameToonPlayVerification.Running";
    static int frames;
    static Renderer flashed;
    static float flashedAt;

    static GameToonPlayVerification()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
    }

    public static void RunBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        // Normal entry is MenuScene, which supplies this persistent manager.
        new GameObject("Verification GameManager").AddComponent<GameManager>();
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static void Tick()
    {
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (++frames < 30) return;
        try
        {
            if (flashed == null)
            {
                Require(GameManager.Instance != null && VFXManager.Instance != null, "Game managers must initialize.");
                var cell = Object.FindObjectsByType<GroundCell>(FindObjectsSortMode.None).First();
                cell.Unlock();
                var renderer = cell.GetComponentInChildren<MeshRenderer>();
                Require(renderer.sharedMaterials.All(m => m.shader.name.StartsWith("Simple Toon/")), "Unlocked ground must use toon materials.");
                Require(renderer.sharedMaterials.Any(m => m.GetColor("_Color").maxColorComponent > 0), "Unlocked ground must retain its visible colors.");
                var modifier = ScriptableObject.CreateInstance<TileModifierSO>();
                modifier.tileColor = Color.red;
                cell.ApplyModifier(modifier);
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                Require(block.GetColor("_BaseColor") == Color.red, "Ground bonus tint must reach renderer.");
                cell.ApplyModifier(null);
                cell.Lock();
                Require(cell.IsLocked && renderer.sharedMaterials.All(m => m.shader.name.StartsWith("Simple Toon/")), "Locked ground must use toon materials.");
                Require(renderer.sharedMaterials.All(m => m.GetColor("_Color").b > m.GetColor("_Color").r && m.GetColor("_Color").maxColorComponent < 0.5f && m.GetFloat("_MinLight") >= 0.4f && m.GetFloat("_ShnIntense") == 0), "Locked ground must retain readable dark blue toon shading without shine.");
                Object.Destroy(modifier);

                var pot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Grass Planter 1x1.prefab"));
                var ghost = pot.GetComponent<GhostController>();
                var renderers = pot.GetComponentsInChildren<MeshRenderer>();
                var originals = renderers.Select(r => r.sharedMaterials).ToArray();
                ghost.SetGhostMode(true); ghost.SetColor(true); ghost.SetGhostMode(false);
                for (int i = 0; i < renderers.Length; i++)
                    Require(originals[i].SequenceEqual(renderers[i].sharedMaterials), "Runtime ghost must restore original toon materials.");

                var plant = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Plants/Plant_Pumpkin.prefab"));
                flashed = plant.GetComponentInChildren<Renderer>();
                Require(flashed.sharedMaterial.HasProperty("_ToonFlash"), "Toon hit flash property missing.");
                GameManager.Instance.StartGame();
                VFXManager.Instance.PlayHitFlash(flashed, Color.white);
                flashed.GetPropertyBlock(block);
                Require(block.GetFloat("_ToonFlash") == 1 && block.GetColor("_ToonFlashColor") == Color.white, "Hit flash must activate.");
                flashedAt = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - flashedAt < 0.5f) return;
            var restored = new MaterialPropertyBlock(); flashed.GetPropertyBlock(restored);
            Require(restored.GetFloat("_ToonFlash") == 0, "Hit flash must clear after its duration.");
            Directory.CreateDirectory("Logs/GameToon");
            File.WriteAllText("Logs/GameToon/play-validation.txt", "PASS: Play Mode initialization, readable dark blue locked ground and colored unlocked ground, tile modifier color/reset, valid placement ghost/restoration, white hit flash activation and timed reset.");
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
            GameToonMigration.Capture(Camera.main, "locked-grid-toon.png");
            Debug.Log("GAME_TOON_PLAY_VALIDATION_PASS");
            Finish(0);
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }

    static void Finish(int result)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(result);
    }
}
