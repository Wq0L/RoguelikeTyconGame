using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Batch: GameScene'de yükseltme ekranı, Temel güç, 60 sn tempo ve Hasat Kotası kuralını oynatarak doğrular.
[InitializeOnLoad]
public static class MechanicsVerification
{
    const string Key = "MechanicsVerification";
    static readonly List<string> notes = new();
    static int step; static double nextAt;
    static RenderTexture target; static Camera cam; static UIManager ui; static GameObject roundEndUI, cardPanel;
    static CardSelectionUI cards;
    static GroundCell upgradedCell; static int levelBefore; static float valueBefore;
    // Kart vignette'inin tasarım değerleri (CardSelectionVignette): renkli köşe vignette'i, tam açıkken 0,65 opak; kart seçimi
    // altın sarısı, boss ödülü mor. Panel her açıldığında şeffaftan başlar, panel kapanınca kapanır.
    const float VignetteAlpha = .65f;
    static readonly Color32 VignetteGold = new Color32(235, 177, 52, 255), VignettePurple = new Color32(153, 87, 224, 255);
    static CardSelectionVignette Vignette => cardPanel.GetComponentInChildren<CardSelectionVignette>(true);
    static Color VignetteColor => Vignette.GetComponent<UnityEngine.UI.RawImage>().color;
    static bool SameTint(Color c, Color32 t) => Mathf.Abs(c.r - t.r / 255f) < .01f && Mathf.Abs(c.g - t.g / 255f) < .01f && Mathf.Abs(c.b - t.b / 255f) < .01f;

    static MechanicsVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        // Yalnız izole kopyada: kullanıcının editörde seçtiği profilden bağımsız, uzun run profiliyle test et.
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>("Assets/Resources/RunProfileSelection.asset");
        selection.active = AssetDatabase.LoadAssetAtPath<RunProfileSO>("Assets/ScriptableObjects/RunProfiles/UzunRun130.asset");
        EditorUtility.SetDirty(selection); AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
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
            if (wait < 0) Finish(null); else nextAt = EditorApplication.timeSinceStartup + wait;
        }
        catch (Exception ex) { Finish(ex); }
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/MechanicsVerification.txt", new[] { ex == null ? "PASS: " + notes.Count + " checks" : "FAIL: " + ex }.Concat(notes));
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(ex == null ? 0 : 1);
    }

    static void Require(bool c, string m) { if (!c) throw new Exception(m); notes.Add("ok: " + m); }
    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void SetF(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, a);
    static void SetP(object o, string n, object v) => o.GetType().GetProperty(n).GetSetMethod(true).Invoke(o, new[] { v });
    static TileModifierSO Mod(string t, string r) => AssetDatabase.LoadAssetAtPath<TileModifierSO>($"Assets/ScriptableObjects/GridModifiers/{t}/{t}-{r}.asset");
    static List<TileCardOffer> Offers() => F<List<TileCardOffer>>(cards, "currentCards");
    static string Strip(string t) => System.Text.RegularExpressions.Regex.Replace(t ?? "", "<[^>]+>", "");

    static double Run(int i)
    {
        switch (i)
        {
            case 0: return Setup();
            case 1: return OpenUpgradeCards();
            case 2: return PickUpgrade();
            case 3: return BaseStats();
            case 4: return Tempo();
            case 5: return Quota();
            case 6: return QuotaHudAndSummary();
            case 7: return QuotaLastRound();
            case 8: return QuotaFail();
            case 9: Save("RunCompleteQuota"); return -1;
            default: return -1;
        }
    }

    static double Setup()
    {
        Require(GameManager.Instance.CurrentState == GameStates.RunSetup, "Scene starts in RunSetup");
        ui = Object.FindFirstObjectByType<UIManager>();
        roundEndUI = F<GameObject>(ui, "roundEndUI");
        cardPanel = F<GameObject>(ui, "cardSelectionPanel");
        cards = cardPanel.GetComponent<CardSelectionUI>();
        new GameObject("Game Feel Director").AddComponent<GameFeelDirector>();
        cam = Camera.main; target = new RenderTexture(1920, 1080, 24); cam.targetTexture = target;
        var canvas = roundEndUI.GetComponentInParent<Canvas>().rootCanvas;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f;

        // 3x3 açık alanı doldur: grid dolu olsun. Ortadaki 2x3 saksı 6 tile'ın üstünde.
        var grid = GridManager.Instance.GetGridSystem(); int c = GridManager.Instance.GetWidth() / 2;
        var data = AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 2x3.asset");
        var cells = new List<GridObject>();
        for (int x = 0; x < 2; x++) for (int z = 0; z < 3; z++) cells.Add(grid.GetGridObject(new GridPosition(c - 1 + x, c - 1 + z)));
        var planter = Object.Instantiate(data.prefab);
        planter.transform.position = cells[0].GetGroundCellCached().transform.position + new Vector3(1, 0, 2);
        foreach (var g in cells) g.SetPlanterObject(planter);
        var brain = planter.GetComponent<PlanterBrain>(); brain.Initialize(data, cells); foreach (var g in cells) g.SetPlanterBrain(brain);
        var types = new[] { Mod("Damage", "Rare"), Mod("Damage", "Common"), Mod("Fertile", "Epic"), Mod("Water", "Rare"), Mod("Energy", "Common"), Mod("Crystal", "Rare"), Mod("Damage", "Epic"), Mod("Water", "Common"), Mod("Fertile", "Rare") };
        foreach (var t in types) Require(ProgressionManager.Instance.ApplyRandomEligibleCell(t), "Card placed " + t.name);
        Require(!ProgressionManager.Instance.HasEligibleCell(), "3x3 open area is full");
        Require(ProgressionManager.Instance.GetUpgradeCandidates(3).All(g => g.Planter != null), "Upgrade candidates come from tiles under planters first");

        RoundManager.Instance.StartNextRound();
        ProgressionManager.Instance.AddXP(605 + 609 + 601 + 1); // 3 level
        Call(RoundManager.Instance, "EndRound");
        Require(GameManager.Instance.CurrentState == GameStates.CardSelection, "Level-ups open card selection");
        Require(Vignette != null && Vignette.gameObject.activeInHierarchy && VignetteColor.a <= .01f, "Card vignette opens with the panel and starts transparent (fade-in): alpha " + VignetteColor.a);
        return 1.5;
    }

    static double OpenUpgradeCards()
    {
        var offers = Offers();
        Require(offers.Count == 3 && offers.All(o => o.IsUpgrade), "Grid full: all three cards are upgrades");
        var banner = cardPanel.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.transform.parent.name == "Grid Full Banner");
        Require(banner != null && banner.transform.parent.gameObject.activeInHierarchy && banner.text.StartsWith("GRİD DOLU"), "Grid full banner shown: " + banner?.text);
        foreach (var o in offers)
            Require(o.LevelGain == Math.Min(GroundCell.LevelsFor(o.Rarity), GroundCell.MaxLevel - o.UpgradeTarget.Level), $"Level gain follows rarity {o.Rarity} → +{o.LevelGain}");
        foreach (var cardUI in cardPanel.GetComponentsInChildren<CardUI>())
        {
            var offer = F<TileCardOffer>(cardUI, "currentOffer"); var rich = F<TextMeshProUGUI>(cardUI, "buffText").text; var buff = Strip(rich);
            var now = offer.UpgradeTarget.RolledModifiers;
            Require(buff.StartsWith($"Sv {offer.UpgradeTarget.Level} → Sv {offer.UpgradeTarget.Level + offer.LevelGain}"), "Card shows level change: " + buff.Replace((char)10, (char)47));
            Require(rich.Contains("<color=#FF8577>") && rich.Contains("<color=#7CF08E>"), "Card colors old red, new green");
            if (now.Count == 1) Require(buff.Contains(TileBuffText.AmountChange(now[0], offer.Modifiers[0])) && buff.Contains(TileBuffText.Amount(now[0]).Replace(" puan", "")), "Card shows old → new value: " + buff.Replace((char)10, (char)47));
        }
        var vignette = Vignette;
        Require(vignette != null && vignette.gameObject.activeInHierarchy && vignette.transform.GetSiblingIndex() == 0 && !vignette.GetComponent<UnityEngine.UI.RawImage>().raycastTarget,
            "Card selection vignette behind the cards, not blocking clicks");
        float designAlpha = (float)typeof(CardSelectionVignette).GetField("MaxAlpha", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();
        Require(Mathf.Abs(designAlpha - VignetteAlpha) < 1e-4f && Mathf.Abs(VignetteColor.a - VignetteAlpha) < .005f,
            $"Vignette faded in to its design opacity {VignetteAlpha}: alpha {VignetteColor.a:0.###} (design constant {designAlpha})");
        Require(SameTint(VignetteColor, VignetteGold), "Card selection vignette is gold: #" + ColorUtility.ToHtmlStringRGB(VignetteColor));
        var bossHost = new GameObject("Boss vignette (verification)", typeof(RectTransform));
        Color bossColor = CardSelectionVignette.Attach((RectTransform)bossHost.transform, CardSelectionVignette.Palette.Boss).GetComponent<UnityEngine.UI.RawImage>().color;
        Object.DestroyImmediate(bossHost);
        Require(SameTint(bossColor, VignettePurple) && bossColor.a <= .01f && !SameTint(bossColor, VignetteGold), "Boss reward vignette is purple and also starts transparent: #" + ColorUtility.ToHtmlStringRGB(bossColor));
        Save("UpgradeCards");
        var pick = offers[0]; upgradedCell = pick.UpgradeTarget; levelBefore = upgradedCell.Level; valueBefore = upgradedCell.RolledModifiers[0].value;
        Call(cards, "OnCardSelected", pick);
        Require(upgradedCell.Level == levelBefore + pick.LevelGain, "Picked tile gained levels: Sv " + upgradedCell.Level);
        float expected = valueBefore * (1 + GroundCell.LevelBonus * upgradedCell.Level) / (1 + GroundCell.LevelBonus * levelBefore);
        Require(Mathf.Abs(upgradedCell.RolledModifiers[0].value - expected) < 1e-4f, "Tile value scaled by +25%/level");
        Require(ProgressionManager.Instance.WasUpgradedThisRound(upgradedCell) && FreshTileRing.LabelFor(upgradedCell) == "+SV", "Upgraded tile tagged +SV for the map");
        return .5;
    }

    static double PickUpgrade()
    {
        // Kalan iki seçimi de yükseltmeyle bitir, sonra tüm tile'ları max'a çıkarıp Temel güç ekranını aç.
        while (GameManager.Instance.CurrentState == GameStates.CardSelection && Offers().Count > 0) Call(cards, "OnCardSelected", Offers()[0]);
        Require(GameManager.Instance.CurrentState == GameStates.RoundEnd, "Round end after the last pick");
        var richRows = roundEndUI.GetComponentInChildren<RoundNewTilesUI>(true).GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.text).ToList();
        var rows = richRows.Select(Strip).ToList();
        Require(!Vignette.gameObject.activeInHierarchy, "Vignette only while choosing cards: gone on the round end screen");
        Require(GameFeelDirector.Instance.LastRound.Xp == 605 + 609 + 601 + 1, "Round summary counts XP earned: " + GameFeelDirector.Instance.LastRound.Xp);
        var summary = Object.FindFirstObjectByType<RoundSummaryUI>(FindObjectsInactive.Include);
        Require(summary != null && summary.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == "XP"), "Round summary shows an XP row");
        string arrow = "→";
        string levelText = $"Sv {levelBefore} {arrow} {upgradedCell.Level}", valueText = arrow + " " + TileBuffText.Amount(upgradedCell.RolledModifiers[0]);
        var upgradeRow = rows.FirstOrDefault(t => t.Contains(levelText) && t.Contains(valueText));
        ProgressionManager.Instance.TryGetRoundUpgrade(upgradedCell, out var record);
        Require(record != null && record.FromLevel == levelBefore && record.ToLevel == upgradedCell.Level && Mathf.Approximately(record.Before[0].value, valueBefore), $"Upgrade record keeps the old state: Sv {record?.FromLevel} → {record?.ToLevel}");
        Require(upgradeRow != null, "New tiles row shows level and value change: " + (upgradeRow ?? string.Join(" | ", rows)).Replace((char)10, (char)47));
        Require(richRows.Any(t => t.Contains("<color=#B8322A>") && t.Contains("<color=#23752F>")), "New tiles row colors old red, new green");
        return 1.5;
    }

    static double BaseStats()
    {
        Save("RoundEndUpgrades");
        var grid = GridManager.Instance.GetGridSystem();
        Tooltips(grid);
        for (int x = 0; x < GridManager.Instance.GetWidth(); x++) for (int z = 0; z < GridManager.Instance.GetHeight(); z++)
        { var g = grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached(); if (g != null && g.CanUpgrade) g.AddLevels(3); }
        Require(ProgressionManager.Instance.GetUpgradeCandidates(3).Count == 0, "Every open tile at max level");
        RoundManager.Instance.StartNextRound();
        ProgressionManager.Instance.AddXP(ProgressionManager.Instance.XPToNextLevel + 1);
        Call(RoundManager.Instance, "EndRound");
        Require(GameManager.Instance.CurrentState == GameStates.CardSelection && Vignette.gameObject.activeInHierarchy && VignetteColor.a <= .01f && SameTint(VignetteColor, VignetteGold),
            "Reopened card selection: the vignette restarts its fade from transparent (no leftover opacity): alpha " + VignetteColor.a);
        var offers = Offers();
        Require(offers.Count == 3 && offers.All(o => o.IsBaseStat), "All tiles max: cards give Temel güç");
        int before = StatManager.Instance.GlobalModifiers.Count;
        var baseOffer = offers[0];
        Call(cards, "OnCardSelected", baseOffer);
        Require(StatManager.Instance.GlobalModifiers.Count == before + baseOffer.Modifiers.Count, "Temel güç added as global modifier: " + baseOffer.BaseStatName);
        return .5;
    }

    // Batch'te imleç yok: iki tooltip'i round sonu panelinde elle doldurup yan yana çek.
    static void Tooltips(GridSystem grid)
    {
        GroundCell levelZero = null;
        for (int x = 0; x < GridManager.Instance.GetWidth() && levelZero == null; x++) for (int z = 0; z < GridManager.Instance.GetHeight(); z++)
        { var g = grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached(); if (g != null && g.CurrentModifier != null && g.Level == 0 && !ProgressionManager.Instance.WasAppliedThisRound(g)) { levelZero = g; break; } }
        Require(levelZero != null, "Found a level-0 tile");
        var canvas = roundEndUI.GetComponentInParent<Canvas>().rootCanvas;
        var shown = new List<GameObject>();
        ComicPopupView Popup(GroundCell cell, float x)
        {
            var host = new GameObject("Verification Tooltip", typeof(RectTransform)); host.layer = canvas.gameObject.layer; shown.Add(host);
            var rect = (RectTransform)host.transform; rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = new Vector2(x, 60);
            var view = ComicPopupView.Attach(host); GridTileToolTipContent.Fill(view, cell); return view;
        }
        var up = Popup(upgradedCell, 260); var zero = Popup(levelZero, -260);
        string upCaption = F<TMP_Text>(up, "subtitle").text, zeroCaption = F<TMP_Text>(zero, "subtitle").text;
        var upRich = F<List<TMP_Text>>(up, "lines").Where(t => t.gameObject.activeSelf).Select(t => t.text).ToList();
        var upLines = upRich.Select(Strip).ToList();
        Require(upRich.Any(t => t.Contains("<color=#9A5B12>")) && upRich.Any(t => t.Contains("<color=#B8322A>")) && upRich.Any(t => t.Contains("<color=#23752F>")), "Tooltip sections and old/new values are colored");
        Require(zeroCaption.Contains($"Sv 0/{GroundCell.MaxLevel}"), "Level-0 tooltip shows the level: " + zeroCaption);
        Require(upCaption.Contains($"Sv {upgradedCell.Level}/{GroundCell.MaxLevel}") && upCaption.Contains("+SV"), "Upgraded tooltip caption: " + upCaption);
        Require(upLines.Any(t => t.Contains($"Sv {levelBefore}") && t.Contains($"Sv {upgradedCell.Level}") && t.Contains("seviye")), "Tooltip says how many levels: " + upLines.FirstOrDefault());
        string change = (TileBuffText.ModifierChanges(ProgressionManager.Instance.TryGetRoundUpgrade(upgradedCell, out var r) ? r.Before : null, upgradedCell.RolledModifiers));
        Require(upLines.Contains(change), "Tooltip lists old → new values: " + change);
        Save("UpgradeTooltip");
        foreach (var go in shown) Object.DestroyImmediate(go);
    }

    // Kart Sezgisi: ağaç tamken kartlar çoğunlukla Epic/Legendary; skill tooltip'i yüzdeyi yazar.
    static void Rarity()
    {
        float W(float luck, int r) => CardSelectionUI.RarityWeight(luck, (TileRarity)r);
        Require(Mathf.Approximately(W(0, 0), 60) && Mathf.Approximately(W(0, 1), 25) && Mathf.Approximately(W(0, 2), 12) && Mathf.Approximately(W(0, 3), 3), "No luck: 60/25/12/3 as before");
        for (float l = 0; l <= .4f; l += .01f) if (Mathf.Abs(W(l, 0) + W(l, 1) + W(l, 2) + W(l, 3) - 100) > .01f) throw new Exception("weights do not sum to 100 at luck " + l);
        notes.Add("ok: Weights sum to 100 for every luck value");
        float treeLuck = AssetDatabase.FindAssets("t:SkillNodeSO", new[] { "Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree" })
            .Select(g => AssetDatabase.LoadAssetAtPath<SkillNodeSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Sum(n => n.tiers[^1].effects.Where(e => e.statType == StatType.MutationLuck).Sum(e => e.value));
        Require(Mathf.Abs(treeLuck - .3f) < .001f, "Tree gives 9 luck tiers: " + treeLuck);
        Require(Mathf.Abs(W(treeLuck, 0) - 3) < .05f && Mathf.Abs(W(treeLuck, 1) - 7) < .05f && Mathf.Abs(W(treeLuck, 2) - 42) < .05f && Mathf.Abs(W(treeLuck, 3) - 48) < .05f, "Full Kart Sezgisi: 3/7/42/48");
        var luckMod = new StatModifier { statType = StatType.MutationLuck, target = StatTarget.Mutation, operation = ModifierOperation.Flat, value = treeLuck };
        StatManager.Instance.AddGlobalModifier(luckMod);
        int n = 20000, low = 0, legend = 0;
        for (int i = 0; i < n; i++) { var r = (TileRarity)Call(cards, "RollRarityTier"); if (r <= TileRarity.Rare) low++; if (r == TileRarity.Legendary) legend++; }
        StatManager.Instance.RemoveGlobalModifier(luckMod);
        Require(Mathf.Abs(low / (float)n - .10f) < .01f && Mathf.Abs(legend / (float)n - .48f) < .015f, $"Live rolls with full tree: Common+Rare {low * 100f / n:0.0}%, Legendary {legend * 100f / n:0.0}%");
        string label = (string)typeof(SkillNodeUI).GetMethod("FormatStat", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { StatType.MutationLuck, treeLuck });
        string start = (string)typeof(SkillNodeUI).GetMethod("FormatStat", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { StatType.MutationLuck, 0f });
        Require(label == "Epic+ %90 · Leg %48" && start == "Epic+ %15 · Leg %3" && TileBuffText.Name(StatType.MutationLuck) == "Kart nadirliği", $"Skill tooltip: Kart nadirliği {start} → {label}");
    }

    static double Tempo()
    {
        Rarity();
        var rm = RoundManager.Instance;
        var extra = new StatModifier { statType = StatType.RoundDuration, target = StatTarget.All, operation = ModifierOperation.Flat, value = 60 };
        StatManager.Instance.AddGlobalModifier(extra);
        Require(Mathf.Approximately(rm.RawRoundDuration, 90) && Mathf.Approximately(rm.EffectiveRoundDuration, 60) && Mathf.Approximately(rm.TempoMultiplier, 1.5f), "90 s of duration skills → 60 s round, 1.5× tempo");
        var spawner = Object.FindObjectsByType<PlantSpawner>(FindObjectsSortMode.None).First();
        float interval = (float)typeof(PlantSpawner).GetMethod("GetEffectiveSpawnInterval", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(spawner, null);
        var brain = spawner.GetComponentInParent<PlanterBrain>();
        Require(Mathf.Abs(interval - Mathf.Max(StatCalculator.MinimumSpawnInterval, brain.GetFinalStat(StatType.PlantSpawnRate)) / 1.5f) < 1e-4f, "Spawn interval sped up by tempo: " + interval);
        StatManager.Instance.RemoveGlobalModifier(extra);
        Require(Mathf.Approximately(rm.TempoMultiplier, 1f) && Mathf.Approximately(rm.EffectiveRoundDuration, rm.RawRoundDuration), "No tempo below 60 s");
        return .2;
    }

    // Hasat Kotası: 5 round'luk segment, kota eğrisi, intro / özet / HUD metinleri, geçiş ve kotayı tutmayınca run sonu.
    static HarvestScoreManager Score => HarvestScoreManager.Instance;
    static void SetScore(long value) => SetF(Score, "totalScore", value);
    static long segmentStart;

    static double Quota()
    {
        Require(HarvestQuota.Target(1, 10, 1.45f) == 10 && HarvestQuota.Target(2, 10, 1.45f) == 15 && HarvestQuota.Target(3, 10, 1.45f) == 20 && HarvestQuota.Target(10, 10, 1.45f) == 280,
            "Quota curve 10 / 15 / 20 … r50 280: " + string.Join(" ", Enumerable.Range(1, 12).Select(s => HarvestQuota.Target(s, 10, 1.45f))));
        Require(HarvestQuota.Nice(1484.3) == 1500 && HarvestQuota.Nice(1099) == 1100 && HarvestQuota.Nice(74) == 75 && HarvestQuota.Format(1234567) == "1.234.567", "Nice rounding and 1.234.567 formatting");
        Require(HarvestQuota.SegmentOf(5, 5) == 1 && HarvestQuota.SegmentOf(6, 5) == 2 && HarvestQuota.SegmentEnd(2, 5) == 10, "Segments are rounds 1-5, 6-10, …");
        var rm = RoundManager.Instance;
        Require(rm.QuotaEnabled && rm.QuotaSegmentRounds == 5 && rm.QuotaTargetFor(1) == 10, "Scene RoundManager uses the default quota (5 rounds, starts at 10)");
        SetP(rm, "CurrentRound", 5);
        rm.StartNextRound();
        segmentStart = Score.TotalScore;
        Require(rm.CurrentRound == 6 && rm.QuotaSegment == 2 && rm.QuotaTarget == 15 && rm.QuotaProgress == 0, "Round 6 opens segment 2 with quota 15 and zero progress");
        Require(GameFeelDirector.QuotaIntro(rm) == "KOTA 15 · 5 ROUND", "Segment start intro: " + GameFeelDirector.QuotaIntro(rm));
        SetScore(segmentStart + 8);
        return .6;
    }

    static double QuotaHudAndSummary()
    {
        var rm = RoundManager.Instance;
        var hud = Object.FindFirstObjectByType<QuotaHUD>(FindObjectsInactive.Include);
        Require(hud != null && hud.gameObject.activeInHierarchy, "Quota HUD attached under the round HUD");
        Call(hud, "Refresh");
        var texts = hud.GetComponentsInChildren<TextMeshProUGUI>(true).ToDictionary(t => t.name, t => t.text);
        Require(texts["Label"] == "KOTA · 5 ROUND" && texts["Value"] == $"{HarvestQuota.Format(rm.QuotaProgress)} / 15", $"HUD: {texts["Label"]} | {texts["Value"]}");
        Save("QuotaHUD");
        SetScore(segmentStart + 8);
        Call(rm, "EndRound");
        Require(!rm.EndedByQuota && rm.LastQuotaRound == 0 && GameManager.Instance.CurrentState == GameStates.RoundEnd, "Mid-segment round end does not judge the quota");
        // Testte GameFeelDirector UIManager'dan sonra abone oldu; oyunda önce abone olur. Özeti round verisiyle yeniden doldur.
        Require(GameFeelDirector.Instance.LastRound.Round == 6, "Round 6 summary data recorded");
        Call(Object.FindFirstObjectByType<RoundSummaryUI>(FindObjectsInactive.Include), "Fill", GameFeelDirector.Instance.LastRound);
        var notice = roundEndUI.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == "Quota Notice");
        Require(notice != null && notice.gameObject.activeInHierarchy && Strip(notice.text).StartsWith("Kota 8 / 15 · 4 round kaldı"), "Round summary shows segment progress: " + notice?.text.Replace((char)10, (char)47));
        return 1.2;
    }

    static double QuotaLastRound()
    {
        Save("QuotaSummary");
        var rm = RoundManager.Instance;
        string last = RoundSummaryUI.QuotaNotice(rm, 9, out var tone);
        Require(last != null && Strip(last).StartsWith("SON ROUND · kotaya 7 kaldı") && tone == RoundSummaryUI.QuotaTone.Danger, "Round 9 summary warns: " + last?.Replace((char)10, (char)47));
        SetP(rm, "CurrentRound", 10);
        Require(GameFeelDirector.QuotaIntro(rm) == "SON ROUND · KOTAYA 7 KALDI", "Round 10 intro: " + GameFeelDirector.QuotaIntro(rm));
        SetP(rm, "CurrentRound", 9);
        GameFeelDirector.Instance.LastRound.Round = 9;
        Call(Object.FindFirstObjectByType<RoundSummaryUI>(FindObjectsInactive.Include), "Fill", GameFeelDirector.Instance.LastRound);
        Save("QuotaSummaryLastRound");
        rm.StartNextRound();
        Require(rm.CurrentRound == 10 && rm.QuotaProgress == 8, "Round 10 keeps segment progress");
        SetScore(segmentStart + 52);
        Call(rm, "EndRound");
        Require(!rm.EndedByQuota && rm.LastQuotaRound == 10 && rm.LastQuotaScore == 52 && rm.LastQuotaTarget == 15 && GameManager.Instance.CurrentState == GameStates.RoundEnd, "52 / 15 passes segment 2");
        Call(Object.FindFirstObjectByType<RoundSummaryUI>(FindObjectsInactive.Include), "Fill", GameFeelDirector.Instance.LastRound);
        string passed = RoundSummaryUI.QuotaNotice(rm, 10, out tone);
        Require(Strip(passed).StartsWith("KOTA TAMAM · 52 / 15") && passed.Contains("Sıradaki kota: 20") && tone == RoundSummaryUI.QuotaTone.Done, "Passed summary: " + passed.Replace((char)10, (char)47));
        return 1.2;
    }

    static double QuotaFail()
    {
        Save("QuotaPassed");
        var rm = RoundManager.Instance;
        rm.StartNextRound();
        long start = Score.TotalScore;
        Require(rm.CurrentRound == 11 && rm.QuotaTarget == 20 && rm.QuotaProgress == 0 && GameFeelDirector.QuotaIntro(rm) == "KOTA 20 · 5 ROUND", "Round 11 resets progress, quota 20");
        SetP(rm, "CurrentRound", 15);
        SetScore(start + 12);
        Call(rm, "EndRound");
        Require(rm.EndedByQuota && GameManager.Instance.CurrentState == GameStates.RunComplete, "12 / 20 at round 15 ends the run");
        var complete = F<GameObject>(ui, "runCompletePanel");
        var text = complete.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.text).FirstOrDefault(t => t.Contains("Harvest Score"));
        Require(text != null && Strip(text).StartsWith("KOTA TUTMADI") && Strip(text).Contains("Round 15 · segment skoru 12 / 20"), "Run complete shows the reason: " + Strip(text).Replace((char)10, (char)47));
        return 1.2;
    }

    static void Save(string name)
    {
        Canvas.ForceUpdateCanvases(); cam.Render();
        var old = RenderTexture.active; RenderTexture.active = target;
        var png = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); png.Apply(); RenderTexture.active = old;
        Directory.CreateDirectory("Logs"); File.WriteAllBytes($"Logs/{name}.png", png.EncodeToPNG()); Object.DestroyImmediate(png);
        notes.Add("saved Logs/" + name + ".png");
    }
}
