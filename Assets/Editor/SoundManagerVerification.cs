using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SoundManagerVerification
{
    private const string Key = "SoundManagerVerification.Stage";
    private static double next;
    private static float savedMusic, savedEffects;
    private static AudioSource music;
    private static float position;
    static SoundManagerVerification() => EditorApplication.update += Step;

    [MenuItem("Tools/Sound/Verify Play Mode %#&v")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save scene before verification.");
        EditorSceneManager.OpenScene("Assets/Scenes/MenuScene.unity");
        SessionState.SetInt(Key, 1);
        EditorApplication.isPlaying = true;
    }

    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    private static void Step()
    {
        int stage = SessionState.GetInt(Key, 0);
        if (stage == 0) return;
        if (stage == 7 && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetInt(Key, 0);
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (stage == 1)
            {
                if (GameManager.Instance == null) return;
                var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuUI>();
                if (menu == null) return;
                savedMusic = GameSettings.MusicVolume;
                savedEffects = GameSettings.EffectsVolume;
                menu.SendMessage("StartRun"); // Same method used by the menu's start selection.
                Advance(2, 3);
            }
            else if (stage == 2)
            {
                var manager = SoundManager.Instance;
                Require(manager != null && manager.gameObject.scene.name == "GameScene", "Scene manager missing");
                Require(UnityEngine.Object.FindObjectsByType<SoundManager>(FindObjectsSortMode.None).Length == 1, "Duplicate manager");
                VerifyMusicPower();
                var names = new[] { "place-item", "cartoon-roguelike-hit-v7", "ui-error", "ui-back" };
                for (int i = 0; i < names.Length; i++)
                {
                    manager.Play((GameSound)i);
                    Require(manager.GetComponents<AudioSource>().Any(s => s.clip != null && s.clip.name == names[i]), "Cue missing: " + names[i]);
                }
                GameSettings.EffectsVolume = 0;
                manager.ApplySettings();
                Require(manager.GetComponents<AudioSource>().Where(s => s.clip != null && names.Contains(s.clip.name)).All(s => s.volume == 0), "Effects mute failed");
                GameSettings.EffectsVolume = savedEffects;
                manager.ApplySettings();
                RoundManager.Instance.StartRound();
                Advance(3, 1);
            }
            else if (stage == 3)
            {
                music = SoundManager.Instance.GetComponents<AudioSource>().Single(s => s.clip != null && s.clip.name == "RoundMusic");
                Require(music.isPlaying && music.loop, "Music did not start looping");
                position = music.time;
                GameManager.Instance.OpenShop();
                Advance(4, 0.5);
            }
            else if (stage == 4)
            {
                Require(!music.isPlaying && Mathf.Abs(music.time - position) < 0.15f, "Music pause lost position");
                GameManager.Instance.StartGame();
                Advance(5, 0.5);
            }
            else if (stage == 5)
            {
                Require(music.isPlaying && music.time > position, "Music resume failed");
                GameSettings.MusicVolume = 0;
                SoundManager.Instance.ApplySettings();
                Require(music.volume == 0 && music.isPlaying, "Music mute restarted/stopped track");
                GameSettings.MusicVolume = savedMusic;
                SoundManager.Instance.ApplySettings();
                Directory.CreateDirectory("Docs/Audio");
                File.WriteAllText("Docs/Audio/PlayModeVerification.txt", "PASS: Menu StartRun -> GameScene; exactly one scene SoundManager; all four cue clips routed; effects mute; round music loop; shop pause preserves position; resume; music mute preserves playback.\nPASS: reward stage points; committed stacks add power; rejected reward adds nothing; clear resets power; inclusive music ranges; unlimited range; weighted selection; invalid and empty pools.\nNot an audible quality test or full placement input test.\n");
                Debug.Log("SoundManager Play Mode verification: PASS");
                Finish();
            }
        }
        catch (Exception error)
        {
            Directory.CreateDirectory("Docs/Audio");
            File.WriteAllText("Docs/Audio/PlayModeVerification.txt", "FAIL: " + error);
            Debug.LogException(error);
            Finish();
        }
    }

    private static void VerifyMusicPower()
    {
        var rewards = BossRewardManager.Instance;
        Require(rewards != null && rewards.MusicPower == 0, "New run power must be zero");
        if (rewards.Pool != null && rewards.Pool.IsStaged)
            for (int i = 0; i < rewards.Pool.stages.Count; i++)
                foreach (var entry in rewards.Pool.stages[i].rewards)
                    Require(rewards.MusicPowerFor(entry.reward) == (entry.reward.musicPowerOverride > 0 ? entry.reward.musicPowerOverride : i + 1), "Wrong reward stage points");
        var reward = ScriptableObject.CreateInstance<BossRewardSO>();
        try
        {
            Require(rewards.MusicPowerFor(reward) == 1, "Unstaged reward must add one");
            reward.musicPowerOverride = 2;
            reward.maxStacks = 2;
            Require(rewards.EditorGrant(reward, out _) && rewards.MusicPower == 2, "First reward power");
            Require(rewards.EditorGrant(reward, out _) && rewards.MusicPower == 4, "Stack reward power");
            Require(!rewards.EditorGrant(reward, out _) && rewards.MusicPower == 4, "Rejected reward changed power");
            Require(SoundManager.Instance.MusicPower == 4, "Sound manager did not read reward power");
        }
        finally { rewards.ClearAll(); UnityEngine.Object.DestroyImmediate(reward); }
        Require(SoundManager.Instance.MusicPower == 0, "Power reset failed");

        var clip = Resources.Load<AudioClip>("Music/RoundMusic");
        var calm = new SoundManager.Entry { clip = clip, minPower = 0, maxPower = 4 };
        var medium = new SoundManager.Entry { clip = clip, minPower = 5, maxPower = 9 };
        var intense = new SoundManager.Entry { clip = clip, minPower = 10, maxPower = 0 };
        var pool = new[] { calm, medium, intense };
        var rng = new System.Random(731);
        foreach (int power in new[] { 0, 4, 5, 9, 10, int.MaxValue })
            Require(SoundManager.SelectMusic(pool, power, rng) == (power < 5 ? calm : power < 10 ? medium : intense), "Power range selection failed: " + power);
        var frequent = new SoundManager.Entry { clip = clip, weight = 3 };
        int selected = 0;
        for (int i = 0; i < 10000; i++)
            if (SoundManager.SelectMusic(new[] { calm, frequent }, 0, rng) == frequent) selected++;
        Require(selected > 7200 && selected < 7800, "Weighted distribution failed");
        Require(SoundManager.SelectMusic(new[] { calm }, 5, rng) == null, "Gap selected an out-of-range track");
        Require(SoundManager.SelectMusic(new[] { new SoundManager.Entry { clip = clip, weight = float.NaN }, new SoundManager.Entry { clip = clip, minPower = 8, maxPower = 2 }, null }, 0, rng) == null, "Invalid entries selected");
        Require(SoundManager.SelectMusic(null, 0, rng) == null, "Null pool failed");
    }

    private static void Advance(int stage, double seconds)
    { SessionState.SetInt(Key, stage); next = EditorApplication.timeSinceStartup + seconds; }
    private static void Finish()
    {
        GameSettings.MusicVolume = savedMusic;
        GameSettings.EffectsVolume = savedEffects;
        SessionState.SetInt(Key, 7);
        EditorApplication.isPlaying = false;
    }
}
