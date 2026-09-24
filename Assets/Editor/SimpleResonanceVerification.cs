using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using ClickerGame.EconomyAnalysis;

[InitializeOnLoad]
public static class SimpleResonanceVerification
{
    const string Key = "SimpleResonanceVerification";
    static readonly List<Object> temporary = new();
    static readonly List<string> notes = new();
    static double captureAt = -1;
    static Camera captureCamera;
    static RenderTexture captureTexture;
    static GameObject gallery;
    static SimpleResonanceVerification() { EditorApplication.update += Tick; }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Use the isolated test project for batch verification.");
        SessionState.SetBool(Key, true);
        EditorApplication.isPaused = false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (captureAt < 0) { Run(); captureAt = EditorApplication.timeSinceStartup + 1; return; }
            if (EditorApplication.timeSinceStartup < captureAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            Capture();
            SessionState.SetBool(Key, false);
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/SimpleResonanceVerification.txt", new[] { "PASS: " + notes.Count + " checks" }.Concat(notes));
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            SessionState.SetBool(Key, false);
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/SimpleResonanceVerification.txt", "FAIL: " + ex);
            Debug.LogException(ex); EditorApplication.Exit(1);
        }
    }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        notes.Add(message);
    }
    static void Equal(double actual, double expected, string message) => Require(Math.Abs(actual - expected) < .001, message + ": " + actual);
    static T Data<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); temporary.Add(value); return value; }
    static GameObject Go(string name) { var go = new GameObject(name); temporary.Add(go); return go; }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    static void Run()
    {
        var rules = Resources.Load<ResonanceRulesSO>("ResonanceRules");
        Require(rules != null && rules.rules.Count == 10, "10 authored rules");
        Require(rules.rules.Select(r => r.id).Distinct().Count() == 10, "Stable unique recipe identities");
        var active = new List<ActiveResonance>(); var mods = new List<StatModifier>();
        var counts = new Dictionary<TileModifierType,int>();
        foreach (var rule in rules.rules)
        {
            foreach (var tier in rule.tiers)
            {
                counts.Clear(); counts[rule.tileType] = tier.requiredCount;
                if (rule.requiresSecondary) counts[rule.secondaryType] = rule.secondaryCount;
                if (rule.requiresAnyBehavior) counts[TileModifierType.Boomerang] = 1;
                ResonanceManager.Evaluate(rules,counts,mods,active);
                Require(active.Any(a => a.id == rule.id && a.requiredCount == tier.requiredCount), rule.id + " threshold " + tier.requiredCount);
                counts[rule.tileType]--;
                ResonanceManager.Evaluate(rules,counts,mods,active);
                Require(!active.Any(a => a.id == rule.id && a.requiredCount == tier.requiredCount), rule.id + " below threshold " + tier.requiredCount);
            }
        }
        counts.Clear();
        foreach(TileModifierType type in Enum.GetValues(typeof(TileModifierType))) counts[type]=4;
        ResonanceManager.Evaluate(rules,counts,mods,active);
        Require(active.Count==10,"All ten recipes coexist without identity collision");
        Equal(StatCalculator.Calculate(1,StatType.GoldGainMultiplier,StatTarget.Planter,null,mods),2,"Resources strongest wins");
        Equal(StatCalculator.Calculate(0,StatType.RareSpawnChance,StatTarget.Planter,null,mods),35,"Rarity strongest wins");
        Equal(StatCalculator.Calculate(.5f,StatType.PlantSpawnRate,StatTarget.Planter,null,mods),.5,"Spawn floor after resonance");
        Equal(ResonanceManager.Multiplier(active,StatType.HarvestScoreMultiplier,PlantRarity.Rare),3,"Rare score does not multiply fame");
        Equal(ResonanceManager.BehaviorMultiplier(active,DamageType.Direct),1,"Behavior bonus never applies to direct");
        foreach(DamageType type in Enum.GetValues(typeof(DamageType)))
            if(type!=DamageType.Direct) Equal(ResonanceManager.BehaviorMultiplier(active,type),2,"Behavior bonus " + type);
        var previous = new List<ActiveResonance>(active);
        Require(active.All(a=>!ResonanceManager.IsNewTier(a,previous)),"Identical refresh has no new popup");
        counts[TileModifierType.Damage]=1; ResonanceManager.Evaluate(rules,counts,mods,active);
        Require(active.All(a=>!ResonanceManager.IsNewTier(a,previous)),"Downgrade does not replay popup");
        var randomBefore=UnityEngine.Random.state;
        ResonanceManager.Evaluate(rules,counts,mods,active);
        Require(JsonUtility.ToJson(randomBefore)==JsonUtility.ToJson(UnityEngine.Random.state),"Evaluation does not consume gameplay RNG");

        var stats=Go("Test stats").AddComponent<StatManager>();
        Set(stats,"coreStatsSO",Data<CoreStatsSO>());
        var resources=Go("Test resources").AddComponent<ResourceManager>();
        var score=Go("Test score").AddComponent<HarvestScoreManager>();
        var progressionGo=Go("Test progression"); progressionGo.SetActive(false);
        var progression=progressionGo.AddComponent<ProgressionManager>();
        Set(progression,"progressionData",Data<ProgressionSO>()); progression.enabled=false; progressionGo.SetActive(true);
        var camera=Go("Preview camera").AddComponent<Camera>(); camera.tag="MainCamera";
        camera.transform.position=new Vector3(0,5,-8);camera.transform.LookAt(Vector3.zero);

        var xpOwner=Planter(TileModifierType.Water,TileModifierType.Water,TileModifierType.Water);
        Equal(xpOwner.GetHarvestXP(10),10,"Source XP replaces target XP resonance rather than multiplying");
        Equal(xpOwner.GetHarvestXP(),2,"Normal target XP resonance retained");
        var electricOwner=Planter(TileModifierType.Water,TileModifierType.Water,TileModifierType.Electric,TileModifierType.Damage);
        Equal(ResonanceManager.ElectricXP(electricOwner.ActiveResonances),10,"Electrical source XP snapshot");
        Equal(electricOwner.GetBehaviorDamage(100,DamageType.Electric),150,"Matching source behavior boosted");
        Equal(electricOwner.GetBehaviorDamage(100,DamageType.Tornado),100,"Unmatched behavior not boosted");

        var energyOwner=Planter(TileModifierType.Energy,TileModifierType.Energy,TileModifierType.Energy,TileModifierType.Crystal,TileModifierType.Crystal,TileModifierType.Fertile);
        Equal(energyOwner.GetHarvestScore(PlantRarity.Common),2,"Normal score resonance");
        Equal(energyOwner.GetHarvestScore(PlantRarity.Rare),3,"Rare score takes strongest");
        Equal(energyOwner.GetFinalStat(StatType.RareSpawnChance),25,"Combined Crystal rarity max");
        Require(energyOwner.ActiveResonances.Count==4,"Four recipes on six-cell footprint");
        Require(energyOwner.TryGetResonancePresentation(new List<ActiveResonance>(),out _,out _,out var message),"Popup generated");
        foreach(var a in energyOwner.ActiveResonances) Require(message.Contains(a.resonanceName),"Popup contains " + a.id);

        foreach(string rarity in new[]{"Common","Rare","Epic","Legendary"})
        {
            var energy=AssetDatabase.LoadAssetAtPath<TileModifierSO>("Assets/ScriptableObjects/GridModifiers/Energy/Energy-"+rarity+".asset");
            Require(energy != null && energy.modifierRanges.All(r=>r.statType==StatType.HarvestScoreMultiplier),"Energy serialized score " + rarity);
        }
        var globalScore=new StatModifier {statType=StatType.HarvestScoreMultiplier,target=StatTarget.All,operation=ModifierOperation.MorePercent,value=1};
        stats.AddGlobalModifier(globalScore);
        Equal(HarvestScoreManager.CalculateAward(PlantRarity.Rare,stats.GetFinalStat(StatType.HarvestScoreMultiplier,StatTarget.Player),energyOwner.GetHarvestScore(PlantRarity.Rare)),60,"Global All score counted once");
        stats.RemoveGlobalModifier(globalScore);

        var plant=Data<PlantSO>(); plant.maxHealth=10;plant.rewardAmount=5;plant.xpAmount=2;plant.resourceType=ResourceType.Gold;plant.rarity=PlantRarity.Rare;
        float xpBefore=progression.CurrentXP; int goldBefore=resources.GetResourceAmount(ResourceType.Gold);int scoreBefore=score.TotalScore;
        var victim=Victim(plant,xpOwner);
        victim.TakeDamage(1,DamageType.Electric,false,10);
        Require(!victim.IsDead,"Nonlethal electric hit");
        victim.TakeDamage(int.MaxValue,DamageType.Direct);
        Equal(progression.CurrentXP-xpBefore,4,"Nonlethal electricity does not taint direct kill XP");
        Equal(resources.GetResourceAmount(ResourceType.Gold)-goldBefore,5,"Single base resource delivery");
        Equal(score.TotalScore-scoreBefore,10,"One score delivery");
        xpBefore=progression.CurrentXP; victim=Victim(plant,xpOwner);
        victim.TakeDamage(int.MaxValue,DamageType.Electric,false,10);
        Equal(progression.CurrentXP-xpBefore,20,"Electric killing hit gets tenfold XP once");
        goldBefore=resources.GetResourceAmount(ResourceType.Gold); victim.TakeDamage(int.MaxValue,DamageType.Electric,false,10);
        Equal(resources.GetResourceAmount(ResourceType.Gold),goldBefore,"Dead plant cannot reward twice");

        var economyOwner=Planter(TileModifierType.Duplicate,TileModifierType.Duplicate,TileModifierType.Fertile);
        var duplicate=new StatModifier {statType=StatType.DuplicateChance,target=StatTarget.Planter,operation=ModifierOperation.Set,value=1};
        stats.AddGlobalModifier(duplicate);
        xpBefore=progression.CurrentXP;goldBefore=resources.GetResourceAmount(ResourceType.Gold);scoreBefore=score.TotalScore;
        victim=Victim(plant,economyOwner);victim.TakeDamage(int.MaxValue);
        Equal(resources.GetResourceAmount(ResourceType.Gold)-goldBefore,20,"Resource resonance x2 plus successful Duplicate x2");
        Equal(progression.CurrentXP-xpBefore,2,"Duplicate is not an XP multiplier");
        Equal(score.TotalScore-scoreBefore,10,"Duplicate is not a score multiplier");
        stats.RemoveGlobalModifier(duplicate);

        var gridSystem=new GridSystem(3,3,1);
        var originGrid=gridSystem.GetGridObject(new GridPosition(0,0));
        var targetGrid=gridSystem.GetGridObject(new GridPosition(1,1));
        foreach(var entry in new[]{originGrid,targetGrid})
        {
            var cell=Go("Electric cell").AddComponent<GroundCell>(); Set(cell,"isLocked",false);
            entry.SetGroundObject(cell.gameObject);
        }
        var burst=Go("Pooled electric fixture").AddComponent<ElectricBurst>();
        xpBefore=progression.CurrentXP;victim=Victim(plant,xpOwner);targetGrid.SetPlantObject(victim.gameObject);
        burst.Launch(null,electricOwner,gridSystem,new[]{new GridPosition(1,1)},new[]{new GridPosition(0,0)},1000);
        Equal(progression.CurrentXP-xpBefore,20,"Actual ElectricBurst transfers source XP to other planter");
        burst.gameObject.SetActive(false);burst.gameObject.SetActive(true);
        var weakElectric=Planter(TileModifierType.Electric);
        xpBefore=progression.CurrentXP;victim=Victim(plant,xpOwner);targetGrid.SetPlantObject(victim.gameObject);
        burst.Launch(null,weakElectric,gridSystem,new[]{new GridPosition(1,1)},new[]{new GridPosition(0,0)},1000);
        Equal(progression.CurrentXP-xpBefore,4,"Pooled electric reuse does not retain previous XP bonus");

        var scythe=Go("Pooled scythe fixture").AddComponent<BoomerangScythe>();
        var combatOwner=Planter(TileModifierType.Damage,TileModifierType.Damage,TileModifierType.Boomerang);
        scythe.Launch(null,combatOwner,Vector3.zero,Vector3.forward*3,100);
        Equal((int)typeof(BoomerangScythe).GetField("damage",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(scythe),130,"Actual orak base damage 65 then resonance x2");
        scythe.gameObject.SetActive(false);scythe.gameObject.SetActive(true);
        scythe.Launch(null,weakElectric,Vector3.zero,Vector3.forward*3,100);
        Equal((int)typeof(BoomerangScythe).GetField("damage",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(scythe),65,"Pooled orak reuse resets source damage");

        var preview=Go("Ghost").AddComponent<ResonancePreviewUI>();
        preview.Show(energyOwner.OccupiedGrids,rules,true);
        Equal(preview.VisibleCount,4,"Ghost displays all four active recipes");
        Require(preview.DetailsVisible,"Placement details start open");
        preview.ToggleDetails(); Require(!preview.DetailsVisible && preview.VisibleCount==4,"Alt toggle hides details independently of badges");
        preview.ToggleDetails(); Require(preview.DetailsVisible,"Details toggle restores panel");
        Require(preview.PreviewResonances.Select(ResonanceManager.Identity).SequenceEqual(energyOwner.ActiveResonances.Select(ResonanceManager.Identity)),"Ghost and placed planter agree");
        preview.Show(energyOwner.OccupiedGrids,rules,false);Equal(preview.VisibleCount,0,"Invalid placement hides badges");
        preview.Show(energyOwner.OccupiedGrids,rules,true);preview.Hide();Equal(preview.VisibleCount,0,"Placement cancellation hides badges");
        preview.Show(energyOwner.OccupiedGrids,rules,true); preview.enabled=false;Equal(preview.VisibleCount,0,"Disabled ghost hides badges");
        preview.enabled=true;
        preview.Show(xpOwner.OccupiedGrids,rules,true);Equal(preview.VisibleCount,1,"Moving ghost removes previous recipes");
        var beforeCount=Object.FindObjectsByType<ResonanceBadgeGraphic>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
        for(int i=0;i<50;i++) preview.Show(energyOwner.OccupiedGrids,rules,true);
        Equal(Object.FindObjectsByType<ResonanceBadgeGraphic>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,beforeCount,"Mouse movement reuses ten badge objects");
        preview.Hide();

        RenderBadges(rules);
        VerifyAnalyzer(rules, plant);
    }
    static void VerifyAnalyzer(ResonanceRulesSO rules, PlantSO plant)
    {
        var profile=Data<EconomyBalanceProfileSO>();profile.planter=Data<PlanterSO>();profile.planter.sizeX=2;profile.planter.sizeZ=3;
        profile.planter.spawnTable=new List<PlantSpawnEntry>{new(){plant=plant,baseChance=100}};
        profile.coreStats=Data<CoreStatsSO>();profile.currentXpCurve=Data<ProgressionSO>();profile.resonanceRules=rules;
        var row=new EconomyRoundAssumption{durationOverride=30,effectiveTargets=6};
        row.extraGlobalModifiers.Add(new StatModifier{statType=StatType.HarvestDamage,target=StatTarget.Player,operation=ModifierOperation.Set,value=100000});
        var types=new[]{TileModifierType.Energy,TileModifierType.Energy,TileModifierType.Energy,TileModifierType.Crystal,TileModifierType.Crystal,TileModifierType.Fertile};
        for(int i=0;i<types.Length;i++) {var tile=Data<TileModifierSO>();tile.modifierType=types[i];row.tiles.Add(new EconomyTile{tile=tile,cell=new Vector2Int(i%2,i/2)});}
        var on=EconomyCalculator.Resolve(profile,row,1,6,true);var off=EconomyCalculator.Resolve(profile,row,1,6,false);
        Equal(on.RareScoreMultiplier,3,"Analyzer resolves Rare+ score");Equal(off.RareScoreMultiplier,1,"Analyzer resonance-off retains baseline score");
        Equal(on.ScoreMultiplier,2,"Analyzer ordinary score");Equal(on.RareBonus,25,"Analyzer strongest rarity contribution");
        off.Round=2;
        var snapshots=new[]{EconomyCalculator.Analyze(on),EconomyCalculator.Analyze(off)};
        var trial=EconomySimulation.RunTrial(snapshots,profile.currentXpCurve,60,713,0);
        var replay=EconomySimulation.RunTrial(snapshots,profile.currentXpCurve,60,713,0);
        Equal(trial[0].Income.Score,trial[0].Income.Harvests*30,"Simulated per-kill Rare+ score");
        Equal(trial[1].Income.Score,trial[1].Income.Harvests*10,"History row uses its own score multiplier");
        Equal(trial[1].CumulativeXp,trial[0].Income.Xp+trial[1].Income.Xp,"Round-by-round XP history");
        Equal(trial[1].Income.Score,replay[1].Income.Score,"Seeded score replay");
        on.DuplicateChance=1;
        var duplicate=EconomySimulation.RunTrial(new[]{EconomyCalculator.Analyze(on)},profile.currentXpCurve,60,713,0)[0];
        Equal(duplicate.Income.Xp,trial[0].Income.Xp,"Simulator Duplicate leaves XP unchanged");
        Equal(duplicate.Income.Score,trial[0].Income.Score,"Simulator Duplicate leaves score unchanged");
        Equal(duplicate.Income.Gold,trial[0].Income.Gold*2,"Simulator Duplicate doubles only resource");
        var result=new EconomySimulationResult();for(int seed=0;seed<12;seed++)result.Trials.Add(EconomySimulation.RunTrial(snapshots,profile.currentXpCurve,60,713,seed));
        var d=result.Distribution(0,r=>r.Income.Score);
        Require(d.P10<=d.P50 && d.P50<=d.P90 && d.Average>0,"Score distribution P10/P50/P90 and average");
    }
    static PlanterBrain Planter(params TileModifierType[] types)
    {
        var grids=new List<GridObject>();
        for(int i=0;i<types.Length;i++)
        {
            var cell=Go("Cell "+i).AddComponent<GroundCell>();
            var grid=new GridObject(null,new GridPosition(i%2,i/2));grid.SetGroundObject(cell.gameObject);cell.SetGridObject(grid);
            var tile=Data<TileModifierSO>();tile.modifierType=types[i]; cell.ApplyModifier(tile);grids.Add(grid);
        }
        var planter=Go("Planter").AddComponent<PlanterBrain>();planter.Initialize(Data<PlanterSO>(),grids);
        foreach(var grid in grids)grid.SetPlanterBrain(planter);
        foreach(var spawner in planter.GetComponentsInChildren<PlantSpawner>())spawner.enabled=false;
        return planter;
    }
    static PlantHealth Victim(PlantSO plant,PlanterBrain owner)
    {
        var go=Go("Reward victim");go.SetActive(false);
        var health=go.AddComponent<PlantHealth>();var resource=go.AddComponent<PlantResource>();Set(resource,"plantHealth",health);
        health.Initialize(plant,owner);resource.Initialize(plant,owner);go.SetActive(true);return health;
    }
    static void RenderBadges(ResonanceRulesSO rules)
    {
        var camera=Go("Badge render camera").AddComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.13f,.16f,.23f);camera.transform.position=new Vector3(0,0,-10);
        var texture=new RenderTexture(1000,360,24);temporary.Add(texture);camera.targetTexture=texture;
        var go=Go("Badge gallery");var canvas=go.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        gallery=go;captureCamera=camera;captureTexture=texture;
        for(int i=0;i<rules.rules.Count;i++)
        {
            var icon=new GameObject(rules.rules[i].id,typeof(RectTransform),typeof(ResonanceBadgeGraphic));
            var rect=icon.GetComponent<RectTransform>();rect.SetParent(go.transform,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
            rect.sizeDelta=new Vector2(120,120);rect.anchoredPosition=new Vector2((i%5-2)*175, i<5?80:-80);
            icon.GetComponent<ResonanceBadgeGraphic>().SetRecipe(rules.rules[i].id);
        }
    }
    static void Capture()
    {
        Canvas.ForceUpdateCanvases();
        foreach(var graphic in gallery.GetComponentsInChildren<ResonanceBadgeGraphic>())
        {
            graphic.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);
            var mesh=graphic.canvasRenderer.GetMesh();
            Require(mesh != null && mesh.vertexCount>100,"Badge mesh built: " + graphic.name);
        }
        captureCamera.Render();
        RenderTexture.active=captureTexture;var png=new Texture2D(1000,360,TextureFormat.RGB24,false);temporary.Add(png);
        png.ReadPixels(new Rect(0,0,1000,360),0,0);png.Apply();RenderTexture.active=null;
        Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/ResonanceBadges.png",png.EncodeToPNG());
        int colors=png.GetPixels().Distinct().Count();
        Require(colors>5,"Rendered badge gallery has colored geometry: " + colors + " colors");
        VerifyPopups();
        foreach(var item in temporary.AsEnumerable().Reverse()) if(item!=null) Object.DestroyImmediate(item);
        temporary.Clear();
    }

    static void VerifyPopups()
    {
        gallery.SetActive(false);
        var texture = new RenderTexture(1500,1000,24); temporary.Add(texture); captureCamera.targetTexture=texture;
        var host=Go("Popup gallery"); var canvas=host.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=captureCamera;canvas.planeDistance=1;
        Canvas.ForceUpdateCanvases();
        var views=new List<ComicPopupView>();
        for(int i=0;i<3;i++)
        {
            var go=new GameObject("Popup "+i,typeof(RectTransform)); go.transform.SetParent(host.transform,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(35+i*480,-35);
            var view=go.AddComponent<ComicPopupView>();views.Add(view);
        }
        var tile=views[0];
        tile.Begin("WATER","RARE");tile.Add("<b>BU TILE</b>");tile.Add("XP kazancı +%35");tile.Add("<b>SAKSININ REZONANSLARI</b>");
        tile.Add("<b>Bilgelik</b>\nXP kazancı ×2","wisdom");tile.Add("<b>Elektrik Bilgisi</b>\nElektrik öldürmelerinde XP ×10","electric-wisdom");tile.End();
        float two=((RectTransform)tile.transform).rect.height;
        tile.Add("<b>Güçlendirilmiş Hasat</b>\nElektrik hasarı ×1,5","empowered-harvest");tile.End();
        Require(((RectTransform)tile.transform).rect.height>two || tile.CanScroll,"Third resonance grows tile popup or enables scrolling at screen limit");
        var skill=views[1];skill.Begin("KESKİN BAŞLANGIÇ - 1","KADEME 1 / 3",skillIcon:"skill-attack");skill.Add("<b>SONRAKİ KADEME</b>");skill.Add("Hasar: 6 → <color=#19745E>8</color>");skill.Add("<b>YÜKSELTME MALİYETİ</b>");skill.Add("5 Gold");skill.End();
        var panel=views[2];panel.Begin("BU KONUMDA AÇILACAK","6 tile · 3 rezonans",footerHint:"ALT: aç / kapat");
        panel.Add("<b>Bilgelik</b>\nXP kazancı ×2","wisdom");panel.Add("<b>Elektrik Bilgisi</b>\nElektrik öldürmelerinde XP ×10","electric-wisdom");panel.Add("<b>Güçlendirilmiş Hasat</b>\nElektrik hasarı ×1,5","empowered-harvest");panel.Add("<b>SAKSININ ALTINDAKİ TILE’LAR</b>");
        for(int i=0;i<6;i++)panel.Add((i+1)+". Water · Rare\nXP kazancı +%35");panel.End();
        var theme=Resources.Load<ComicUITheme>("ComicUITheme");
        Require(theme != null && theme.headingFont != null && theme.outlinedText != null,"Popup toon theme available");
        var turkish=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Art/UI/ComicToon/Fonts/HarvestComicTR SDF.asset");
        Require(turkish!=null && turkish.atlasPopulationMode==TMPro.AtlasPopulationMode.Static,"Turkish comic glyphs are pre-baked");
        foreach(char c in "ÇçĞğİıÖöŞşÜü")
        {
            var character=TMPro.TMP_FontAssetUtilities.GetCharacterFromFontAsset(c,theme.headingFont,true,TMPro.FontStyles.Normal,TMPro.FontWeight.Regular,out bool alternate);
            Require(character!=null && (character.textAsset==theme.headingFont || character.textAsset==turkish),"Turkish character stays in comic face: "+c);
        }
        Require(host.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>t.font==theme.headingFont && t.fontSharedMaterial==theme.headingFont.material && t.color==ComicPopupView.Ink),"Every popup uses toon font with solid dark ink on cream paper");
        Require(skill.transform.Find("Skill portrait").gameObject.activeSelf,"Skill category icon visible before title");
        var keys=Enum.GetValues(typeof(StatType)).Cast<StatType>().Select(SkillIconCatalog.For).Concat(new[]{"skill-unlock"}).Distinct().ToArray();
        for(int i=0;i<keys.Length;i++)
        {
            var icon=new GameObject(keys[i],typeof(RectTransform),typeof(ResonanceBadgeGraphic));var rect=(RectTransform)icon.transform;rect.SetParent(host.transform,false);
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(38+(i%11)*130,-570-(i/11)*145);rect.sizeDelta=new Vector2(94,94);
            icon.GetComponent<ResonanceBadgeGraphic>().SetRecipe(keys[i]);
        }
        int nodes=0;
        foreach(var guid in AssetDatabase.FindAssets("t:SkillNodeSO",new[]{"Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree"}))
        {
            var node=AssetDatabase.LoadAssetAtPath<SkillNodeSO>(AssetDatabase.GUIDToAssetPath(guid));
            Require(keys.Contains(SkillIconCatalog.For(node)),"Authored skill has supported icon: "+node.nodeName);nodes++;
        }
        Require(nodes>=140,"Complete authored skill tree checked");
        var unlock=Data<SkillNodeSO>();unlock.unlockType=UnlockType.TileBehavior_Tornado;
        Require(SkillIconCatalog.For(unlock)=="skill-tornado","Behavior unlock uses its own pictogram");
        if(TooltipManager.Instance==null) Go("Tooltip manager fixture").AddComponent<TooltipManager>();
        var nodePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Skill Node.prefab");
        Require(nodePrefab!=null,"Real skill node prefab loaded");
        var actualNode=Object.Instantiate(nodePrefab,host.transform);
        var nodeRect=(RectTransform)actualNode.transform;nodeRect.anchorMin=nodeRect.anchorMax=new Vector2(0,1);nodeRect.anchoredPosition=new Vector2(110,-890);nodeRect.localScale=Vector3.one*1.5f;
        Require(!actualNode.transform.Find("LockOverlay").gameObject.activeSelf,"Actual prefab no longer washes out its icon");
        Require(actualNode.transform.Find("IconImage").localScale==Vector3.one,"Actual prefab icon uses full scale");
        Require(actualNode.transform.Find("Comic shadow").GetSiblingIndex()<actualNode.transform.Find("Comic face").GetSiblingIndex(),"Comic face renders in front of shadow");
        Require(actualNode.GetComponent<UnityEngine.UI.Button>().transition==UnityEngine.UI.Selectable.Transition.None,"Legacy button tint cannot wash out node");
        var specimen=new GameObject("Turkish specimen",typeof(RectTransform));specimen.transform.SetParent(host.transform,false);
        var specimenRect=(RectTransform)specimen.transform;specimenRect.anchorMin=specimenRect.anchorMax=new Vector2(0,1);specimenRect.pivot=new Vector2(0,1);specimenRect.anchoredPosition=new Vector2(250,-830);specimenRect.sizeDelta=new Vector2(1200,160);
        specimen.SetActive(false);
        var specimenText=specimen.AddComponent<TMPro.TextMeshProUGUI>();theme.StylePopupText(specimenText);specimenText.color=Color.white;specimenText.fontSize=36;
        specimenText.text="Çç Ğğ İı Öö Şş Üü — Gg Ii Ss\nGüçlü Başlangıç · ŞİMŞEK · Ağaç · İleri Gelişim";
        specimen.SetActive(true);
        Canvas.ForceUpdateCanvases();
        foreach(var text in host.GetComponentsInChildren<TMPro.TMP_Text>())text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases(); captureCamera.Render();
        RenderTexture.active=texture;var png=new Texture2D(1500,1000,TextureFormat.RGB24,false);temporary.Add(png);png.ReadPixels(new Rect(0,0,1500,1000),0,0);png.Apply();RenderTexture.active=null;
        File.WriteAllBytes("Logs/ComicPopups.png",png.EncodeToPNG());
        panel.Begin("ON REZONANS");for(int i=0;i<10;i++)panel.Add("Rezonans "+i+"\nUzun açıklama: hasat ve kaynak bonusu.","wisdom");panel.End();
        Require(panel.RowCount==10 && panel.CanScroll,"Ten resonances retained with clipped scrolling");
        panel.Scroll(100000);var content=(RectTransform)panel.transform.Find("Viewport/Content");Require(content.anchoredPosition.y>0,"Long popup scroll reaches later entries");
        panel.Begin("KISA");panel.Add("Tek bonus");panel.End();Require(!panel.CanScroll && content.anchoredPosition.y==0,"Pooled popup shrinks and resets scroll");
        Require(panel.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>!t.raycastTarget),"Presentation never blocks placement input");
        skill.Begin("YENİ TILE");skill.Add("Bonus");skill.End();
        Require(!skill.transform.Find("Skill portrait").gameObject.activeSelf,"Pooled popup clears previous skill icon");
    }
}

