using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Functional coverage only: scores are injected, this is not a human balance/playfeel test.
[InitializeOnLoad]
public static class DemoVerification
{
    const string Key = "DemoVerification.Running";
    static int phase, round = 1, errors;
    static double next;
    static string original;
    static readonly System.Collections.Generic.List<string> notes = new();
    static DemoVerification() { EditorApplication.update += Tick; }
    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/DemoScene.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true) };
        EditorSceneManager.OpenScene("Assets/Scenes/DemoScene.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        notes.Add("PASS " + message);
    }
    static object Call(object obj, string method, params object[] args) => obj.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(obj, args);
    static void Field(object obj, string name, object value) => obj.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    static void Log(string message, string trace, LogType type)
    {
        if (trace.Contains("UnityEditor.Search.SearchDatabase") && !trace.Contains("Assets/Scripts/"))
        {
            notes.Add("EDITOR INFRASTRUCTURE (not gameplay): " + message);
            return;
        }
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; notes.Add(message); }
    }
    static string SourceSnapshot()
    {
        var source = AssetDatabase.LoadAssetAtPath<RunProfileSO>("Assets/ScriptableObjects/RunProfiles/Run50_AlphaDengeV1.asset");
        return JsonUtility.ToJson(source) + JsonUtility.ToJson(source.balance) +
            JsonUtility.ToJson(source.balance.plantHealth) + JsonUtility.ToJson(source.bossRewards);
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (phase == 0)
            {
                Application.logMessageReceived += Log;
                next = EditorApplication.timeSinceStartup + 3;
                phase++; return;
            }
            var rm = RoundManager.Instance;
            if (phase == 1)
            {
                Check(DemoSceneSettings.IsDemo && rm.MaxRounds == 20, "DemoScene starts standalone with 20 rounds");
                var demo = DemoSceneSettings.Instance;
                original = SourceSnapshot();
                Check(rm.Profile != demo.sourceProfile && rm.Profile.balance != demo.sourceProfile.balance &&
                    rm.Profile.balance.plantHealth != demo.sourceProfile.balance.plantHealth, "Runtime copies isolate original assets");
                Check(demo.sourceProfile.runLength == 50 && demo.sourceProfile.bossRewards.stages[2].firstRound == 22 &&
                    demo.sourceProfile.balance.plantHealth.anchors[0].commonHealth == 20, "Full profile retained 50 rounds, R22 rewards and original HP");
                Check(rm.Profile.balance.plantHealth.anchors[0].commonHealth == 15, "Demo common HP reduced by 25 percent");
                Check(rm.QuotaTargetFor(1) == 20 && rm.BossTargetFor(1) == 5, "Demo quota and boss goals halved");
                Check(string.Join(",", rm.Profile.bossCalendar.Select(d => d.round)) == "3,6,10,13,16,20", "Six expected boss rounds");
                Check(rm.CalendarError == null && rm.DurationError == null && rm.RewardPoolError == null, "Calendar, duration, reward validation");
                var catalog = StartCatalogSO.Active;
                MetaSave.UseMemoryOnly();
                var farmer = catalog.farmers.FirstOrDefault(x => x != catalog.defaultFarmer);
                var scythe = catalog.scythes.FirstOrDefault(x => x != catalog.defaultScythe);
                if (farmer != null) { MetaSave.Data.farmer = farmer.id; MetaSave.Data.unlocked.Add(farmer.id); }
                if (scythe != null) { MetaSave.Data.scythe = scythe.id; MetaSave.Data.unlocked.Add(scythe.id); }
                StartLoadoutManager.Instance.Apply();
                Check(StartLoadoutManager.Instance.Farmer == catalog.defaultFarmer && StartLoadoutManager.Instance.Scythe == catalog.defaultScythe,
                    "Even unlocked saved farmer/scythe cannot bypass demo defaults");
                Check(!(bool)typeof(QuestTracker).GetField("counting", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(QuestTracker.Instance),
                    "Demo does not progress permanent unlock quests");
                foreach (var type in new[] { UnlockType.TileBehavior_Tornado, UnlockType.TileBehavior_Boomerang })
                {
                    UnlockManager.Instance.Unlock(type);
                    Check(!UnlockManager.Instance.IsUnlocked(type), "Unlock guard " + type);
                }
                var nodes = SkillTreeManager.Instance.AllNodes.Where(DemoSceneSettings.Blocks).ToArray();
                Check(nodes.Length >= 2, "Both locked behavior nodes exist");
                foreach (var node in nodes) Check(!SkillTreeManager.Instance.TryUpgrade(node), "Skill cannot be purchased: " + node.name);
                foreach (string guid in AssetDatabase.FindAssets("t:TileModifierSO"))
                {
                    var tile = AssetDatabase.LoadAssetAtPath<TileModifierSO>(AssetDatabase.GUIDToAssetPath(guid));
                    if (DemoSceneSettings.Blocks(tile.modifierType)) Check(!tile.IsAvailableInCardPool, "Card excluded: " + tile.name);
                }
                Check(UnlockManager.Instance.IsUnlocked(UnlockType.TileBehavior_Explosive) && UnlockManager.Instance.IsUnlocked(UnlockType.TileBehavior_Electric),
                    "Explosion and electricity remain available");
                var play = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == "Play Demo");
                Capture("DemoWelcome");
                play.onClick.Invoke();
                phase++; next = EditorApplication.timeSinceStartup + 1; return;
            }
            if (phase == 2)
            {
                if (round == 1) rm.StartRound(); else rm.StartNextRound();
                Check(rm.CurrentRound == round && rm.IsRoundActive, "Start round " + round);
                Field(HarvestScoreManager.Instance, "totalScore", HarvestScoreManager.Instance.TotalScore + 100000L);
                Call(rm, "EndRound");
                if (BossRewardManager.Instance.IsPending)
                {
                    var offer = BossRewardManager.Instance.Offer.ToArray();
                    Check(offer.Length > 0, "Reward offer at R" + round);
                    if (round == 13 || round == 16)
                        Check(offer.All(r => rm.Profile.bossRewards.StageIndexOf(r) == 2), "Only strong rewards at R" + round);
                    Check(BossRewardManager.Instance.Choose(offer[0]), "Boss reward accepted at R" + round);
                }
                if (round++ < 20) { next = EditorApplication.timeSinceStartup + .1; return; }
                Check(rm.Outcome == RunOutcome.Victory && GameManager.Instance.CurrentState == GameStates.RunComplete, "R20 ends in victory");
                rm.StartNextRound();
                Check(rm.CurrentRound == 20 && !rm.IsRoundActive, "Cannot enter R21 after victory");
                Check(SourceSnapshot() == original, "Runtime changes did not mutate full-game source assets");
                phase = 6; next = EditorApplication.timeSinceStartup + 2; return;
            }
            if (phase == 6)
            {
                Capture("DemoVictory");
                Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnMainMenuPressed();
                phase = 3; next = EditorApplication.timeSinceStartup + 3; return;
            }
            if (phase == 3)
            {
                Check(SceneManager.GetActiveScene().name == "DemoScene" && rm.MaxRounds == 20 && rm.CurrentRound == 1, "Menu return reloads demo, not full game");
                Object.FindFirstObjectByType<RunCompleteUI>(FindObjectsInactive.Include).OnRestartPressed();
                phase++; next = EditorApplication.timeSinceStartup + 3; return;
            }
            if (phase == 4)
            {
                Check(DemoSceneSettings.IsDemo && rm.MaxRounds == 20, "Restart retains demo settings");
                SceneManager.LoadScene("GameScene");
                phase++; next = EditorApplication.timeSinceStartup + 3; return;
            }
            if (phase == 5)
            {
                Check(!DemoSceneSettings.IsDemo && rm.MaxRounds == 50, "Loading GameScene restores full-game profile");
                UnlockManager.Instance.Unlock(UnlockType.TileBehavior_Tornado);
                UnlockManager.Instance.Unlock(UnlockType.TileBehavior_Boomerang);
                Check(UnlockManager.Instance.IsUnlocked(UnlockType.TileBehavior_Tornado) && UnlockManager.Instance.IsUnlocked(UnlockType.TileBehavior_Boomerang),
                    "Full-game tornado and boomerang still unlock");
                Check(errors == 0, "No runtime errors");
                Finish(null);
            }
        }
        catch (Exception e) { Finish(e.ToString()); }
    }
    static void Capture(string name)
    {
        Camera cam = Camera.main;
        var target = new RenderTexture(1280, 720, 24);
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1; }
        var oldTarget = cam.targetTexture; var oldActive = RenderTexture.active;
        cam.targetTexture = target; Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = target;
        var png = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); png.Apply();
        Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + name + ".png", png.EncodeToPNG());
        cam.targetTexture = oldTarget; RenderTexture.active = oldActive;
        foreach (var canvas in canvases) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Object.DestroyImmediate(png); Object.DestroyImmediate(target);
    }
    static void Finish(string error)
    {
        SessionState.SetBool(Key, false);
        if (error != null) notes.Add("FAIL " + error);
        Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/DemoVerification.txt", notes);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
