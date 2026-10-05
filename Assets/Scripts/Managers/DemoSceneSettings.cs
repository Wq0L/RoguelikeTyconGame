using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Only scenes with this component use demo rules. Source assets are never modified.
[DefaultExecutionOrder(-20000)]
[DisallowMultipleComponent]
public sealed class DemoSceneSettings : MonoBehaviour
{
    public static DemoSceneSettings Instance { get; private set; }
    public static bool IsDemo => Instance != null;
    [Header("Demo configuration (applied when Play starts)")]
    public RunProfileSO sourceProfile;
    [Range(3, 50)] public int roundCount = 20;
    [Min(1)] public int middleRewardsFromRound = 6;
    [Min(1)] public int strongRewardsFromRound = 13;
    [Range(0.1f, 1f)] public float quotaMultiplier = 0.5f;
    [Range(0.1f, 1f)] public float bossTargetMultiplier = 0.5f;
    [Range(0.1f, 1f)] public float plantHealthMultiplier = 0.75f;
    public bool lockTornado = true;
    public bool lockBoomerang = true;
    public bool defaultLoadoutOnly = true;
    public bool showWelcome = true;
    public RunProfileSO Profile { get; private set; }
    private readonly List<UnityEngine.Object> copies = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private T Copy<T>(T source) where T : UnityEngine.Object
    {
        T copy = Instantiate(source);
        copy.name = source.name + " (Demo runtime)";
        copies.Add(copy);
        return copy;
    }

    private void Awake()
    {
        Instance = this;
        if (sourceProfile == null || sourceProfile.balance == null || sourceProfile.balance.plantHealth == null ||
            sourceProfile.bossRewards == null || sourceProfile.bossRewards.stages.Count != 3)
            throw new InvalidOperationException("Demo requires a run profile with health data and three reward stages.");
        Profile = Copy(sourceProfile);
        Profile.displayName = "DEMO · " + roundCount + " round";
        Profile.runLength = roundCount;
        Profile.debugBudget = false;
        Profile.victoryTitle = "DEMO TAMAMLANDI!";
        Profile.specializationAfterSegment = 0;
        Profile.bossCalendar = new List<BossDate>();
        Profile.segmentTargets = new List<long>();
        Profile.bossTargets = new List<long>();
        foreach (BossDate date in sourceProfile.bossCalendar)
        {
            if (date.round >= roundCount) break;
            AddDate(date.round, false, date.boss);
        }
        AddDate(roundCount, true, null);
        Profile.roundDurations.RemoveAll(band => band.fromRound > roundCount);
        Profile.balance = Copy(sourceProfile.balance);
        Profile.balance.plantHealth = Copy(sourceProfile.balance.plantHealth);
        foreach (PlantHealthAnchor anchor in Profile.balance.plantHealth.anchors)
            anchor.commonHealth = Mathf.Max(1f, anchor.commonHealth * plantHealthMultiplier);
        Profile.balance.startingUnlocks.RemoveAll(Blocks);
        Profile.balance.firstBehaviorOffer.RemoveAll(Blocks);
        Profile.bossRewards = Copy(sourceProfile.bossRewards);
        Profile.bossRewards.stages[1].firstRound = middleRewardsFromRound;
        Profile.bossRewards.stages[2].firstRound = strongRewardsFromRound;
        // Only the current stage is offered: R13 and R16 really offer strong rewards.
        Profile.bossRewards.stageDistanceWeights = new List<float> { 1f, 0f, 0f };
        Profile.bossRewards.reserveCurrentStageSlot = true;
        foreach (BossRewardStage stage in Profile.bossRewards.stages)
            stage.rewards.RemoveAll(entry => Blocks(entry.reward));
        string error = RunCalendar.Validate(Profile) ?? RoundDurations.Validate(Profile) ?? BossRewardPoolSO.Validate(Profile.bossRewards, roundCount);
        if (error != null) throw new InvalidOperationException("Demo configuration: " + error);
        if (GameManager.Instance == null) new GameObject("Demo Game Manager").AddComponent<GameManager>();
    }

    private void AddDate(int round, bool final, SegmentEventSO boss)
    {
        int period = sourceProfile.Calendar.PeriodOf(round);
        Profile.bossCalendar.Add(new BossDate { round = round, final = final, boss = boss });
        Profile.segmentTargets.Add(Math.Max(5L, (long)Math.Ceiling(sourceProfile.TargetFor(period) * quotaMultiplier)));
        Profile.bossTargets.Add(Math.Max(1L, (long)Math.Ceiling(sourceProfile.BossTargetFor(period) * bossTargetMultiplier)));
    }

    public static bool Blocks(UnlockType type) => IsDemo &&
        ((Instance.lockTornado && type == UnlockType.TileBehavior_Tornado) ||
         (Instance.lockBoomerang && type == UnlockType.TileBehavior_Boomerang));
    public static bool Blocks(StatType type) => IsDemo &&
        ((Instance.lockTornado && type == StatType.TornadoChance) ||
         (Instance.lockBoomerang && type == StatType.BoomerangChance));
    public static bool Blocks(TileModifierType type) => IsDemo &&
        ((Instance.lockTornado && type == TileModifierType.Tornado) ||
         (Instance.lockBoomerang && type == TileModifierType.Boomerang));
    public static bool Blocks(SkillNodeSO node)
    {
        if (!IsDemo || node == null) return false;
        if (Blocks(node.unlockType)) return true;
        foreach (SkillNodeTier tier in node.tiers)
            foreach (StatModifier modifier in tier.effects)
                if (Blocks(modifier.statType)) return true;
        return false;
    }
    public static bool Blocks(BossRewardSO reward)
    {
        if (!IsDemo || reward == null) return false;
        if (reward.condition == BossRewardCondition.AnyPlanterHasConditionStat && Blocks(reward.conditionStat)) return true;
        // Mixed buffs remain useful for the allowed behaviors; their locked chances are forced to zero.
        return reward.modifiersPerStack.Count > 0 && reward.modifiersPerStack.TrueForAll(m => Blocks(m.statType));
    }

    public void ReturnToDemo() => SceneManager.LoadScene(gameObject.scene.name);

    private void Start()
    {
        if (!showWelcome) return;
        var root = new GameObject("Demo Welcome", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var panel = new GameObject("Background", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color32(24, 29, 43, 255);
        Label(panel.transform, "Earth Is Gone.\nLunch Isn't.", new Vector2(0, 210), new Vector2(1500, 240), 78);
        Label(panel.transform, "ERKEN ALFA DEMO · " + roundCount + " ROUND\n\n" +
            "Hasat et, geliştir, güçlü kombinasyonlar kur.\n" +
            "R" + strongRewardsFromRound + "'ten itibaren güçlü boss ödülleri.\n" +
            (defaultLoadoutOnly ? "Başlangıç: varsayılan çiftçi ve tırpan.\n" : "") +
            (lockTornado && lockBoomerang ? "Kasırga ve bumerang tam sürümde." : ""),
            new Vector2(0, -40), new Vector2(1500, 280), 32);
        var button = new GameObject("Play Demo", typeof(RectTransform), typeof(Image), typeof(Button));
        button.transform.SetParent(panel.transform, false);
        var buttonRect = (RectTransform)button.transform;
        buttonRect.sizeDelta = new Vector2(460, 100); buttonRect.anchoredPosition = new Vector2(0, -310);
        button.GetComponent<Image>().color = new Color32(61, 126, 85, 255);
        Label(button.transform, "DEMOYU OYNA", Vector2.zero, new Vector2(460, 100), 36);
        button.GetComponent<Button>().onClick.AddListener(() => Destroy(root));
    }

    private static void Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.sizeDelta = size; rect.anchoredPosition = position;
        var text = go.GetComponent<TextMeshProUGUI>();
        var theme = FeelOverlay.Theme;
        var font = theme != null ? (fontSize >= 60 ? theme.headingFont : theme.bodyFont) : null;
        if (font != null) { text.font = font; text.fontSharedMaterial = font.material; }
        text.text = value; text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (UnityEngine.Object copy in copies) if (copy != null) Destroy(copy);
    }
}
