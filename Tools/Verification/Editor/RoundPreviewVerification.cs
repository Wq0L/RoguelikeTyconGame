using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Batch: GameScene'i oynatır; saksı kurar, round oynatır, kart uygular, round sonu / önizleme / skill ağacı PNG'lerini çeker.
[InitializeOnLoad]
public static class RoundPreviewVerification
{
    const string Key = "RoundPreviewVerification";
    static readonly List<string> notes = new();
    static int step;
    static double nextAt;
    static RenderTexture target;
    static Camera cam;
    static UIManager ui;
    static GameObject roundEndUI, skillPanel;
    static RectTransform resourcesUI;
    static Transform resourcesHome;
    static int plantRenderersBefore;
    static readonly List<GroundCell> planterCells = new();

    static RoundPreviewVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        SessionState.SetInt(Key + "Step", 0);
        // Oyunun URP ayarı (bu test projesinde varsayılan boş); Finish geri alır.
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        // GameManager normalde MenuScene'den DontDestroyOnLoad ile gelir.
        if (Object.FindAnyObjectByType<GameManager>() == null) new GameObject("Game Manager (verification)").AddComponent<GameManager>();
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (nextAt == 0) nextAt = EditorApplication.timeSinceStartup + 2;
            if (EditorApplication.timeSinceStartup < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            double wait = Run(step++);
            if (wait < 0) Finish(null);
            else nextAt = EditorApplication.timeSinceStartup + wait;
        }
        catch (Exception ex) { Finish(ex); }
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/RoundPreviewVerification.txt",
            new[] { ex == null ? "PASS: " + notes.Count + " checks" : "FAIL: " + ex }.Concat(notes));
        if (ex != null) Debug.LogException(ex);
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null;
        QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        notes.Add("ok: " + message);
    }

    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(target, args);
    static TileModifierSO Mod(string type, string rarity) =>
        AssetDatabase.LoadAssetAtPath<TileModifierSO>($"Assets/ScriptableObjects/GridModifiers/{type}/{type}-{rarity}.asset");

    static double Run(int index)
    {
        switch (index)
        {
            case 0: return Setup();
            case 1: return EndRound();
            case 2: return CaptureRoundEnd();
            case 3: return CapturePreview();
            case 4: return ClosePreviewAndOpenSkills();
            case 5: return CaptureSkills();
            case 6: return NextRoundClears();
            default: return -1;
        }
    }

    static double Setup()
    {
        Require(GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.RunSetup, "Scene starts in RunSetup");
        ui = Object.FindFirstObjectByType<UIManager>();
        roundEndUI = Field<GameObject>(ui, "roundEndUI");
        skillPanel = Field<GameObject>(ui, "skillShopPanel");
        resourcesUI = Field<RectTransform>(ui, "resourcesUI");
        Require(resourcesUI != null && resourcesUI.name == "ResourcesUI", "UIManager found the resource counters: " + resourcesUI?.name);
        resourcesHome = resourcesUI.parent;
        Require(roundEndUI.activeInHierarchy, "Round end panel open in RunSetup");
        Require(roundEndUI.transform.Find("Preview Button") != null, "Preview button attached to round end panel");
        Require(roundEndUI.GetComponentInChildren<RoundNewTilesUI>(true) != null, "New tiles card attached");

        // Batch'te FeelBootstrap çalışmaz; oyundaki gibi ekle.
        new GameObject("Fresh Tile Markers").AddComponent<FreshTileMarkers>();
        new GameObject("Game Feel Director").AddComponent<GameFeelDirector>();

        cam = Camera.main;
        target = new RenderTexture(1920, 1080, 24);
        cam.targetTexture = target;
        var canvas = roundEndUI.GetComponentInParent<Canvas>().rootCanvas;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;

        GridUnlockManager.Instance.UnlockNextTier(7);
        var grid = GridManager.Instance.GetGridSystem();
        int cx = GridManager.Instance.GetWidth() / 2, cz = GridManager.Instance.GetHeight() / 2;
        // 2x3 saksı: 3 Damage + 2 Energy + 1 Fertile; 1x2 saksı: 2 Water.
        Place("GrassPlanter 2x3", grid, cx - 3, cz - 1, new[] { Mod("Damage", "Rare"), Mod("Damage", "Common"), Mod("Damage", "Epic"), Mod("Energy", "Rare"), Mod("Energy", "Common"), Mod("Fertile", "Legendary") });
        Place("GrassPlanter 1x2", grid, cx + 2, cz - 1, new[] { Mod("Water", "Rare"), Mod("Water", "Common") });
        Place("GrassPlanter 1x1", grid, cx, cz + 2, new TileModifierSO[] { null });

        ResourceManager.Instance.AddResource(ResourceType.Gold, 1250);
        ResourceManager.Instance.AddResource(ResourceType.Iron, 48);
        ResourceManager.Instance.AddResource(ResourceType.Stone, 97);
        RoundManager.Instance.StartNextRound();
        Require(GameManager.Instance.CurrentState == GameStates.Round, "Round started");
        return 6;
    }

    static void Place(string asset, GridSystem grid, int ox, int oz, TileModifierSO[] mods)
    {
        var data = AssetDatabase.LoadAssetAtPath<PlanterSO>($"Assets/ScriptableObjects/Planters/{asset}.asset");
        var cells = new List<GridObject>();
        for (int x = 0; x < data.sizeX; x++) for (int z = 0; z < data.sizeZ; z++) cells.Add(grid.GetGridObject(new GridPosition(ox + x, oz + z)));
        Require(cells.All(c => c != null && !c.GetGroundCellCached().IsLocked && !c.HasPlanterObject()), asset + " footprint is free and unlocked");
        var planter = Object.Instantiate(data.prefab);
        float size = GridManager.Instance.GetCellSize();
        planter.transform.position = cells[0].GetGroundCellCached().transform.position + new Vector3((data.sizeX - 1) * size * .5f, 0, (data.sizeZ - 1) * size * .5f);
        foreach (var c in cells) c.SetPlanterObject(planter);
        var brain = planter.GetComponent<PlanterBrain>();
        brain.Initialize(data, cells);
        foreach (var c in cells) c.SetPlanterBrain(brain);
        for (int i = 0; i < cells.Count && i < mods.Length; i++)
            if (mods[i] != null) cells[i].GetGroundCellCached().ApplyModifier(mods[i]);
        planterCells.Add(cells[0].GetGroundCellCached());
    }

    static double EndRound()
    {
        var pool = PlantPool.ForScene(roundEndUI.scene);
        plantRenderersBefore = pool.GetComponentsInChildren<Renderer>().Count(r => r.enabled);
        Require(plantRenderersBefore > 0, "Plants grew during the round: " + plantRenderersBefore + " renderers");
        GameManager.Instance.ShowRoundEnd();
        // Kart seçimi: 3 kart rastgele boş tile'lara.
        UnityEngine.Random.InitState(11);
        Require(ProgressionManager.Instance.ApplyRandomEligibleCell(Mod("Crystal", "Epic")), "Card 1 applied");
        Require(ProgressionManager.Instance.ApplyRandomEligibleCell(Mod("Fertile", "Rare")), "Card 2 applied");
        Require(ProgressionManager.Instance.ApplyRandomEligibleCell(Mod("Tornado", "Legendary")), "Card 3 applied");
        Require(ProgressionManager.Instance.RoundAppliedCells.Count == 3, "Round list has 3 cells");
        ui.ShowRoundEndUI(); // harita yeni listeyle yeniden kurulur
        return 2;
    }

    static double CaptureRoundEnd()
    {
        var rings = roundEndUI.GetComponentsInChildren<FreshTileRing>(false);
        Require(rings.Length == 3, "Map highlights exactly the 3 fresh cells: " + rings.Length);
        var fresh = ProgressionManager.Instance.RoundAppliedCells;
        foreach (var ring in rings)
        {
            var cell = ring.transform.parent.name; // Cell_x{x}_z{z}
            Require(fresh.Any(c => cell == $"Cell_x{c.GetGridPosition().x}_z{c.GetGridPosition().z}"), "Ring sits on a fresh cell " + cell);
        }
        var card = roundEndUI.GetComponentInChildren<RoundNewTilesUI>(true);
        Require(card.transform.Find("Card").gameObject.activeSelf, "New tiles card visible");
        var markers = Object.FindFirstObjectByType<FreshTileMarkers>();
        Require(markers.GetComponentsInChildren<MeshRenderer>(false).Length == 3, "World markers on 3 fresh tiles");
        notes.Add($"roundEnd alpha={roundEndUI.GetComponent<CanvasGroup>()?.alpha} frame={Time.frameCount} unscaled={Time.unscaledTime}");
        Save("RoundEnd");
        var rootCanvas = roundEndUI.GetComponentInParent<Canvas>().rootCanvas;
        rootCanvas.enabled = false;
        Save("World");
        rootCanvas.enabled = true;
        var ground = planterCells[0];
        notes.Add($"camera pos={cam.transform.position} rot={cam.transform.eulerAngles} ortho={cam.orthographic} fov={cam.fieldOfView} size={cam.orthographicSize} near={cam.nearClipPlane} far={cam.farClipPlane} mask={cam.cullingMask} clear={cam.clearFlags} bg={cam.backgroundColor}");
        notes.Add($"ground pos={ground.transform.position} renderer={ground.GroundRenderer?.enabled} mat={ground.GroundRenderer?.sharedMaterial?.name} shader={ground.GroundRenderer?.sharedMaterial?.shader?.name} layer={ground.gameObject.layer} screen={cam.WorldToScreenPoint(ground.transform.position)}");
        ui.OpenRoundPreview();
        Require(RoundPreviewUI.IsOpen && !roundEndUI.activeSelf, "Preview replaces the round end panel");
        return 1.2;
    }

    static double CapturePreview()
    {
        var pool = PlantPool.ForScene(roundEndUI.scene);
        Require(pool.GetComponentsInChildren<Renderer>().All(r => !r.enabled), "All plant renderers hidden in preview");
        var preview = Object.FindFirstObjectByType<RoundPreviewUI>();
        int markers = Field<int>(preview, "activeMarkers"), chips = Field<int>(preview, "activeChips"), tags = Field<int>(preview, "activeTags");
        Require(markers == 2, "Two planters with resonances get badge rows: " + markers);
        Require(chips >= 3, "Resonance strip lists the active recipes: " + chips);
        Require(tags == 3, "Three YENİ tags in world");
        Require(GameManager.Instance.CurrentState == GameStates.RoundEnd, "Preview does not change game state");
        var group = preview.GetComponent<CanvasGroup>(); var nested = preview.GetComponent<Canvas>();
        notes.Add($"preview alpha={group?.alpha} canvas={nested?.enabled} scale={preview.transform.localScale} frame={Time.frameCount} unscaled={Time.unscaledTime} ts={Time.timeScale}");
        var roundGroup = roundEndUI.GetComponent<CanvasGroup>();
        // Tile bilgisi: imleç batch'te yok; paneli elle doldur ve ekranın sağ ortasına koy.
        Call(preview, "SetDetails", planterCells[0]);
        var details = Field<RectTransform>(preview, "detailsRect");
        Require(details.gameObject.activeSelf, "Details popup shows for a planter tile");
        details.GetComponent<ComicPopupView>().Fit();
        details.pivot = new Vector2(1, 1);
        details.anchoredPosition = new Vector2(1880, 900);
        Canvas.ForceUpdateCanvases();
        cam.Render();
        Save("Preview");
        ui.CloseRoundPreview();
        Require(!RoundPreviewUI.IsOpen && roundEndUI.activeSelf, "Back returns to the round end panel");
        Require(pool.GetComponentsInChildren<Renderer>().Count(r => r.enabled) == plantRenderersBefore, "Plant renderers restored");
        Call(ui, "ToggleRoundPreview");
        Require(RoundPreviewUI.IsOpen, "TAB opens preview");
        Call(ui, "ToggleRoundPreview");
        Require(!RoundPreviewUI.IsOpen && roundEndUI.activeSelf, "TAB closes preview");
        return 0.5;
    }

    static double ClosePreviewAndOpenSkills()
    {
        ui.OpenSkillShop();
        Require(resourcesUI.parent == skillPanel.transform && resourcesUI.GetSiblingIndex() == skillPanel.transform.childCount - 1, "Counters pinned on top of the skill tree");
        Require(resourcesUI.gameObject.activeInHierarchy, "Counters visible in the skill tree");
        return 1.5;
    }

    static double CaptureSkills()
    {
        Save("SkillTree");
        // Karşılaştırma: eski (parlak) arka plan değerleri.
        var space = skillPanel.GetComponentInChildren<SkillTreeSpaceBackground>(true);
        Require(space != null && space.enabled, "Skill tree space background active");
        var before = new Dictionary<string, object>
        {
            ["deepColor"] = new Color(0.035f, 0.035f, 0.1f), ["centerGlow"] = new Color(0.13f, 0.09f, 0.26f),
            ["nebulaIntensity"] = 0.3f, ["galaxyIntensity"] = 0.35f, ["starIntensity"] = 1f, ["starDensity"] = 0.45f, ["vignette"] = 0.5f
        };
        var saved = new Dictionary<string, object>();
        foreach (var pair in before)
        {
            var field = typeof(SkillTreeSpaceBackground).GetField(pair.Key, BindingFlags.NonPublic | BindingFlags.Instance);
            saved[pair.Key] = field.GetValue(space); field.SetValue(space, pair.Value);
        }
        Call(space, "ApplyProperties");
        Save("SkillTreeBefore");
        foreach (var pair in saved) typeof(SkillTreeSpaceBackground).GetField(pair.Key, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(space, pair.Value);
        Call(space, "ApplyProperties");
        GameManager.Instance.ShowRoundEnd();
        Require(resourcesUI.parent == resourcesHome, "Counters return home after closing the skill tree");
        ui.OpenSkillShop();
        ui.CloseAll();
        Require(resourcesUI.parent == resourcesHome, "CloseAll returns counters home");
        GameManager.Instance.ShowRoundEnd();
        return 0.5;
    }

    static double NextRoundClears()
    {
        ui.NextRound();
        Require(GameManager.Instance.CurrentState == GameStates.Round, "Next round started");
        Require(ProgressionManager.Instance.RoundAppliedCells.Count == 0, "New round clears the fresh list");
        return 0.5;
    }

    static void Save(string name)
    {
        Canvas.ForceUpdateCanvases();
        cam.Render();
        var old = RenderTexture.active;
        RenderTexture.active = target;
        var png = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        png.Apply();
        RenderTexture.active = old;
        Directory.CreateDirectory("Logs");
        File.WriteAllBytes($"Logs/{name}.png", png.EncodeToPNG());
        Object.DestroyImmediate(png);
        notes.Add("saved Logs/" + name + ".png");
    }
}
